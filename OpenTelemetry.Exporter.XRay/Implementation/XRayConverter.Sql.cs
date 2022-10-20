using System;
using System.Collections.Generic;

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

            var url = dbUrl + "/" + dbInstance;

            var writer = context.Writer;
            writer.WritePropertyName(XRayWriter.Sql);
            writer.WriteStartObject();

            writer.WriteString(XRayWriter.Url, url);
            if (dbSystem != null)
                writer.WriteString(XRayWriter.DatabaseType, dbSystem);
            if (dbUser != null)
                writer.WriteString(XRayWriter.User, dbUser);
            if (dbStatement != null)
                writer.WriteString(XRayWriter.SanitizedQuery, dbStatement);

            writer.WriteEndObject();
        }
    }
}