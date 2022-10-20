using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal struct XRayResponseData
    {
        [JsonPropertyName("status")]
        public long? Status { get; set; }
        
        [JsonPropertyName("content_length")]
        public long? ContentLength { get; set; }
    }
}