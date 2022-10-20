using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayLogGroupMetadata
    {
        [JsonPropertyName("log_group")]
        public string LogGroup { get; set; }

        [JsonPropertyName("arn")]
        public string Arn { get; set; }
    }
}