using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayStackFrame
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("line")]
        public int? Line { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }
    }
}