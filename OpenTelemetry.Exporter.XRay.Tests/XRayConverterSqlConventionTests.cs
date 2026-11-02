using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public partial class XRayConverterSqlTests
    {
        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(2, true)]
        public void Should_prefer_enabled_modern_database_attributes_independent_of_tag_order(int mode, bool reverse)
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeDbSystemName] = "postgresql",
                [XRayConventions.AttributeDbNamespace] = "orders",
                [XRayConventions.AttributeDbQueryText] = "SELECT 1",
                [XRayConventions.AttributeUserName] = "new_user",
                [XRayConventions.AttributeDbSystem] = "mysql",
                [XRayConventions.AttributeDbName] = "customers",
                [XRayConventions.AttributeDbStatement] = "SELECT 2",
                [XRayConventions.AttributeDbUser] = "legacy_user",
            };
            using var activity = ConstructSqlConventionSpan(reverse ? tags.Reverse() : tags);
            var converter = CreateSqlConventionConverter(mode);

            // Reuse the converter and its thread-local cache as well as varying insertion order.
            for (var i = 0; i < 3; i++)
            {
                var sql = ConvertSegment(converter, activity).Sql;
                Assert.NotNull(sql);
                Assert.Equal(mode == 0 ? "mysql" : "postgresql", sql.DatabaseType);
                Assert.Equal(mode == 0 ? "localhost/customers" : "localhost/orders", sql.ConnectionString);
                Assert.Equal(mode == 0 ? "SELECT 2" : "SELECT 1", sql.SanitizedQuery);
                Assert.Equal(mode == 0 ? "legacy_user" : "new_user", sql.User);
                Assert.Equal("users.findUnique", sql.Url);
            }
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        public void Should_gate_modern_only_database_attributes(int mode, bool hasSql)
        {
            using var activity = ConstructSqlConventionSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeDbSystemName] = "postgresql",
                [XRayConventions.AttributeDbNamespace] = "orders",
                [XRayConventions.AttributeDbQueryText] = "SELECT 1",
                [XRayConventions.AttributeUserName] = "new_user",
                [XRayConventions.AttributeServerAddress] = "db.example.com",
            });

            var sql = ConvertSegment(CreateSqlConventionConverter(mode), activity).Sql;
            if (!hasSql)
            {
                Assert.Null(sql);
            }
            else
            {
                Assert.NotNull(sql);
                Assert.Equal("localhost/orders", sql.ConnectionString);
                Assert.Equal("postgresql", sql.DatabaseType);
                Assert.Equal("SELECT 1", sql.SanitizedQuery);
                Assert.Equal("new_user", sql.User);
            }
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        public void Should_retain_legacy_database_fallback_unless_disabled(int mode, bool hasSql)
        {
            using var activity = ConstructSqlConventionSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeDbSystem] = "mysql",
                [XRayConventions.AttributeDbName] = "customers",
                [XRayConventions.AttributeDbStatement] = "SELECT 2",
                [XRayConventions.AttributeDbUser] = "legacy_user",
            });

            var sql = ConvertSegment(CreateSqlConventionConverter(mode), activity).Sql;
            if (!hasSql)
            {
                Assert.Null(sql);
            }
            else
            {
                Assert.NotNull(sql);
                Assert.Equal("mysql", sql.DatabaseType);
                Assert.Equal("localhost/customers", sql.ConnectionString);
                Assert.Equal("SELECT 2", sql.SanitizedQuery);
                Assert.Equal("legacy_user", sql.User);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(true)]
        [InlineData(42)]
        public void Should_fall_back_from_empty_or_wrong_type_modern_database_attributes(object invalid)
        {
            using var activity = ConstructSqlConventionSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeDbSystemName] = invalid,
                [XRayConventions.AttributeDbNamespace] = invalid,
                [XRayConventions.AttributeDbQueryText] = invalid,
                [XRayConventions.AttributeUserName] = invalid,
                [XRayConventions.AttributeDbSystem] = "mysql",
                [XRayConventions.AttributeDbName] = "customers",
                [XRayConventions.AttributeDbStatement] = "SELECT 2",
                [XRayConventions.AttributeDbUser] = "legacy_user",
            });
            var sql = ConvertSegment(CreateSqlConventionConverter(1), activity).Sql;
            Assert.NotNull(sql);
            Assert.Equal("mysql", sql.DatabaseType);
            Assert.Equal("localhost/customers", sql.ConnectionString);
            Assert.Equal("SELECT 2", sql.SanitizedQuery);
            Assert.Equal("legacy_user", sql.User);
            Assert.Null(ConvertSegment(CreateSqlConventionConverter(2), activity).Sql);
        }

        [Theory]
        [InlineData("ibm.db2")]
        [InlineData("microsoft.sql_server")]
        [InlineData("oracle.db")]
        public void Should_gate_new_sql_system_aliases_on_either_system_key(string system)
        {
            foreach (var key in new[] { XRayConventions.AttributeDbSystem, XRayConventions.AttributeDbSystemName })
            {
                using var activity = ConstructSqlConventionSpan(new Dictionary<string, object> { [key] = system });
                Assert.Null(ConvertSegment(CreateSqlConventionConverter(0), activity).Sql);
                Assert.Equal(system, ConvertSegment(CreateSqlConventionConverter(1), activity).Sql.DatabaseType);

                var sql = ConvertSegment(CreateSqlConventionConverter(2), activity).Sql;
                if (key == XRayConventions.AttributeDbSystem)
                    Assert.Null(sql);
                else
                    Assert.Equal(system, sql.DatabaseType);
            }
        }

        [Theory]
        [InlineData("db2")]
        [InlineData("derby")]
        [InlineData("hive")]
        [InlineData("mariadb")]
        [InlineData("mssql")]
        [InlineData("mysql")]
        [InlineData("oracle")]
        [InlineData("postgresql")]
        [InlineData("sqlite")]
        [InlineData("teradata")]
        [InlineData("other_sql")]
        public void Should_retain_existing_sql_system_names(string system)
        {
            using var legacy = ConstructSqlConventionSpan(new Dictionary<string, object> { [XRayConventions.AttributeDbSystem] = system });
            Assert.Equal(system, ConvertSegment(CreateSqlConventionConverter(0), legacy).Sql.DatabaseType);
            Assert.Equal(system, ConvertSegment(CreateSqlConventionConverter(1), legacy).Sql.DatabaseType);

            using var modern = ConstructSqlConventionSpan(new Dictionary<string, object> { [XRayConventions.AttributeDbSystemName] = system });
            Assert.Equal(system, ConvertSegment(CreateSqlConventionConverter(2), modern).Sql.DatabaseType);
        }

        [Theory]
        [InlineData("redis")]
        [InlineData("mongodb")]
        [InlineData("MYSQL")]
        [InlineData("")]
        public void Should_not_treat_modern_non_sql_system_as_legacy_sql(string system)
        {
            using var activity = ConstructSqlConventionSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeDbSystemName] = system,
                [XRayConventions.AttributeDbSystem] = "mysql",
            });

            var sql = ConvertSegment(CreateSqlConventionConverter(1), activity).Sql;
            if (system == "")
                Assert.Equal("mysql", sql.DatabaseType);
            else
                Assert.Null(sql);
        }

        [Theory]
        [InlineData(0, "mysql://db.example.com:3306/customers")]
        [InlineData(1, "mysql://db.example.com:3306/orders")]
        [InlineData(2, "localhost/orders")]
        public void Should_keep_legacy_connection_string_unless_disabled(int mode, string expected)
        {
            using var activity = ConstructSqlConventionSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeDbSystem] = "mysql",
                [XRayConventions.AttributeDbSystemName] = "mysql",
                [XRayConventions.AttributeDbName] = "customers",
                [XRayConventions.AttributeDbNamespace] = "orders",
                [XRayConventions.AttributeDbConnectionString] = "mysql://db.example.com:3306",
            });
            Assert.Equal(expected, ConvertSegment(CreateSqlConventionConverter(mode), activity).Sql.ConnectionString);
        }

        [Fact]
        public void Should_consume_only_enabled_sql_attributes_and_preserve_non_sql_metadata()
        {
            using var activity = ConstructSqlConventionSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeDbSystem] = "mysql",
                [XRayConventions.AttributeDbSystemName] = "postgresql",
                [XRayConventions.AttributeDbNamespace] = "orders",
                [XRayConventions.AttributeDbName] = "customers",
                [XRayConventions.AttributeDbConnectionString] = "legacy-connection",
                [XRayConventions.AttributeEndUserId] = "end_user",
            });

            using var legacy = JsonDocument.Parse(CreateSqlConventionConverter(0, false).Convert(Resource.Empty, activity));
            var metadata = legacy.RootElement.GetProperty("metadata").GetProperty("default");
            Assert.Equal("postgresql", metadata.GetProperty("db.system.name").GetString());
            Assert.Equal("orders", metadata.GetProperty("db.namespace").GetString());
            Assert.False(metadata.TryGetProperty("db.system", out _));

            using var modern = JsonDocument.Parse(CreateSqlConventionConverter(2, false).Convert(Resource.Empty, activity));
            metadata = modern.RootElement.GetProperty("metadata").GetProperty("default");
            Assert.Equal("mysql", metadata.GetProperty("db.system").GetString());
            Assert.Equal("customers", metadata.GetProperty("db.name").GetString());
            Assert.Equal("legacy-connection", metadata.GetProperty("db.connection_string").GetString());
            Assert.False(metadata.TryGetProperty("db.system.name", out _));

            using var additive = JsonDocument.Parse(CreateSqlConventionConverter(1, false).Convert(Resource.Empty, activity));
            Assert.False(additive.RootElement.TryGetProperty("metadata", out _));

            activity.SetTag(XRayConventions.AttributeDbSystemName, "redis");

            using var nonSql = JsonDocument.Parse(CreateSqlConventionConverter(1, false).Convert(Resource.Empty, activity));
            Assert.False(nonSql.RootElement.TryGetProperty("sql", out _));
            Assert.Equal("redis", nonSql.RootElement.GetProperty("metadata").GetProperty("default").GetProperty("db.system.name").GetString());
        }

        [Fact]
        public void Should_clear_cached_modern_database_values_between_conversions()
        {
            var converter = CreateSqlConventionConverter(1);
            using (var activity = ConstructSqlConventionSpan(new Dictionary<string, object>
                   {
                       [XRayConventions.AttributeDbSystemName] = "postgresql",
                       [XRayConventions.AttributeDbNamespace] = "orders",
                       [XRayConventions.AttributeDbQueryText] = "SELECT 1",
                       [XRayConventions.AttributeUserName] = "new_user",
                   }))
                Assert.Equal("new_user", ConvertSegment(converter, activity).Sql.User);

            using var next = ConstructSqlConventionSpan(new Dictionary<string, object> { [XRayConventions.AttributeDbSystem] = "mysql" });
            var sql = ConvertSegment(converter, next).Sql;
            Assert.Equal("mysql", sql.DatabaseType);
            Assert.Equal("localhost/", sql.ConnectionString);
            Assert.Null(sql.SanitizedQuery);
            Assert.Null(sql.User);
        }

        private Activity ConstructSqlConventionSpan(IEnumerable<KeyValuePair<string, object>> tags)
        {
            var activity = ActivitySource.CreateActivity("users.findUnique", ActivityKind.Client, new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded), tags);
            Assert.NotNull(activity);
            return activity;
        }

        private static XRayConverter CreateSqlConventionConverter(int mode, bool indexAllAttributes = true)
        {
            return new XRayConverter(null, null, indexAllAttributes, false, emitV1DbConventions: mode != 0, dontEmitV0DbConventions: mode == 2);
        }
    }
}