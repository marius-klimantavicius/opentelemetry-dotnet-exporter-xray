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
            var dbSystem = default(string);
            var dbInstance = default(string);
            var dbStatement = default(string);
            var dbUser = default(string);

            var spanTags = context.SpanTags;
            if (spanTags.TryGetAttributeDbConnectionString(out var value))
                dbUrl = value.AsString();

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

            if (string.IsNullOrEmpty(dbUrl))
                dbUrl = "localhost";

            var writer = context.Writer;
            writer.WritePropertyName(XRayField.Sql);
            writer.WriteStartObject();

            WriteDatabaseUrl(writer, dbUrl, dbInstance);
            if (dbSystem != null)
                writer.WriteString(XRayField.DatabaseType, dbSystem);
            if (dbUser != null)
                writer.WriteString(XRayField.User, dbUser);
            if (dbStatement != null)
                writer.WriteString(XRayField.SanitizedQuery, dbStatement);

            writer.WriteEndObject();
        }

        private void WriteDatabaseUrl(Utf8JsonWriter writer, string dbUrl, string dbInstance)
        {
            var sb = new ValueStringBuilder(stackalloc char[128]);
            sb.Append(dbUrl);
            sb.Append('/');
            sb.Append(dbInstance);

            writer.WriteString(XRayField.Url, sb.AsSpan());
            sb.Dispose();
        }
    }
}