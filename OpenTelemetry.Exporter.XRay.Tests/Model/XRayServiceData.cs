using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayServiceData
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("compiler_version")]
        public string CompilerVersion { get; set; }

        [JsonPropertyName("compiler")]
        public string Compiler { get; set; }
    }
}