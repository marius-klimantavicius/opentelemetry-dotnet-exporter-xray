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

        private void WriteSql(in XRayConverterContext context)
        {
            var dbUrl = default(string);
            var dbConnectionString = default(string);
            var dbSystem = default(string);
            var dbInstance = default(string);
            var dbStatement = default(string);
            var dbUser = default(string);

            var spanTags = context.SpanTags;
            if (spanTags.TryGetAttributeDbConnectionString(out var value))
                dbConnectionString = value.AsString();

            if (spanTags.TryGetAttributeDbSystem(out value))
                dbSystem = value.AsString();

            if (spanTags.TryGetAttributeDbName(out value))
                dbInstance = value.AsString();

            if (spanTags.TryGetAttributeDbStatement(out value))
                dbStatement = value.AsString();

            if (spanTags.TryGetAttributeDbUser(out value))
                dbUser = value.AsString();

            if (!_sqlSystems.Contains(dbSystem))
                return;

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