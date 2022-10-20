using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayMetaData
    {
        [JsonPropertyName("sdk")]
        public string Sdk { get; set; }

        [JsonPropertyName("sdk_version")]
        public string SdkVersion { get; set; }

        [JsonPropertyName("auto_instrumentation")]
        public bool? AutoInstrumentation { get; set; }
    }
}