using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private static readonly HashSet<string> _sqlSystems = new HashSet<string>(StringComparer.Ordinal)
        {
            "db2",
            "derby",
            "hive",
            "mariadb",
            "mssql",
            "mysql",
            "oracle",
            "postgresql",
            "sqlite",
            "teradata",
            "other_sql",
        };

        private static readonly HashSet<string> _sqlV1Systems = new HashSet<string>(StringComparer.Ordinal)
        {
            "ibm.db2",
            "microsoft.sql_server",
            "oracle.db",
        };

        private void WriteSql(in XRayConverterContext context)
        {
            var dbUrl = default(string);
            var dbConnectionString = default(string);
            var dbSystem = default(string);
            var dbInstance = default(string);
            var dbStatement = default(string);
            var dbUser = default(string);

            var spanTags = context.SpanTags;
            if (_emitV1DbConventions)
            {
                if (spanTags.TryGetAttributeDbSystemName(out var modernValue))
                    dbSystem = modernValue.AsString();
                if (spanTags.TryGetAttributeDbNamespace(out modernValue))
                    dbInstance = modernValue.AsString();
                if (spanTags.TryGetAttributeDbQueryText(out modernValue))
                    dbStatement = modernValue.AsString();
                if (spanTags.TryGetAttributeUserName(out modernValue))
                    dbUser = modernValue.AsString();
            }

            if (!_dontEmitV0DbConventions)
            {
                if (spanTags.TryGetAttributeDbConnectionString(out var value))
                    dbConnectionString = value.AsString();

                if (spanTags.TryGetAttributeDbSystem(out value) && string.IsNullOrEmpty(dbSystem))
                    dbSystem = value.AsString();

                if (spanTags.TryGetAttributeDbName(out value) && string.IsNullOrEmpty(dbInstance))
                    dbInstance = value.AsString();

                if (spanTags.TryGetAttributeDbStatement(out value) && string.IsNullOrEmpty(dbStatement))
                    dbStatement = value.AsString();

                if (spanTags.TryGetAttributeDbUser(out value) && string.IsNullOrEmpty(dbUser))
                    dbUser = value.AsString();
            }

            if (!_sqlSystems.Contains(dbSystem) && !(_emitV1DbConventions && _sqlV1Systems.Contains(dbSystem)))
            {
                // Non-SQL database attributes remain available for annotations and metadata.
                spanTags.ResetConsume();
                return;
            }

            // Despite what the X-Ray documents say, having the DB connection string
            // set as the URL value of the segment is not useful. So let's use the
            // current span name instead
            dbUrl = context.Span.DisplayName;

            // Let's keep the original format for connection_string
            if (string.IsNullOrEmpty(dbConnectionString))
                dbConnectionString = "localhost";

            var writer = context.Writer;
            writer.WritePropertyName(XRayField.Sql);
            writer.WriteStartObject();


            writer.WriteString(XRayField.Url, dbUrl);
            WriteDatabaseConnectionString(writer, dbConnectionString, dbInstance);
            if (dbSystem != null)
                writer.WriteString(XRayField.DatabaseType, dbSystem);
            if (dbUser != null)
                writer.WriteString(XRayField.User, dbUser);
            if (dbStatement != null)
                writer.WriteString(XRayField.SanitizedQuery, dbStatement);

            writer.WriteEndObject();
            spanTags.Consume();
        }

        private void WriteDatabaseConnectionString(Utf8JsonWriter writer, string dbUrl, string dbInstance)
        {
            var sb = new ValueStringBuilder(stackalloc char[128]);
            sb.Append(dbUrl);
            sb.Append('/');
            sb.Append(dbInstance);

            writer.WriteString(XRayField.ConnectionString, sb.AsSpan());
            sb.Dispose();
        }
    }
}