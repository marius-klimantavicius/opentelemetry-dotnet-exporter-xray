using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    public class XRaySqlData
    {
        [JsonPropertyName("connection_string")]
        public string ConnectionString { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("sanitized_query")]
        public string SanitizedQuery { get; set; }

        [JsonPropertyName("database_type")]
        public string DatabaseType { get; set; }

        [JsonPropertyName("database_version")]
        public string DatabaseVersion { get; set; }

        [JsonPropertyName("driver_version")]
        public string DriverVersion { get; set; }

        [JsonPropertyName("user")]
        public string User { get; set; }

        [JsonPropertyName("preparation")]
        public string Preparation { get; set; }
    }
}