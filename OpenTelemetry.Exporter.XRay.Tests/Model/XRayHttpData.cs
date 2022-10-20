using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayHttpData
    {
        [JsonPropertyName("request")]
        public XRayRequestData Request { get; set; }

        [JsonPropertyName("response")]
        public XRayResponseData Response { get; set; }
    }
}