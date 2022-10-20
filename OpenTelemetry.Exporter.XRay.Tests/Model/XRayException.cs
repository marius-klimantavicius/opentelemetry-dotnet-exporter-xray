using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayException
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("remote")]
        public bool? Remote { get; set; }

        [JsonPropertyName("truncated")]
        public long? Truncated { get; set; }

        [JsonPropertyName("skipped")]
        public long? Skipped { get; set; }

        [JsonPropertyName("cause")]
        public string Cause { get; set; }

        [JsonPropertyName("stack")]
        public List<XRayStackFrame> Stack { get; set; }
    }
}