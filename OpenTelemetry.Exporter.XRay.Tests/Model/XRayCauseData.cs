using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayCauseData
    {
        [JsonPropertyName("working_directory")]
        public string WorkingDirectory { get; set; }

        [JsonPropertyName("paths")]
        public IEnumerable<string> Paths { get; set; }

        [JsonPropertyName("exceptions")]
        public IEnumerable<XRayException> Exceptions { get; set; }
    }
}