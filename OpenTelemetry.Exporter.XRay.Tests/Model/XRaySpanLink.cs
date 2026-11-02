using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRaySpanLink
    {
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("attributes")]
        public IDictionary<string, object> Attributes { get; set; }
    }
}