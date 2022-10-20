using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayEC2Metadata
    {
        [JsonPropertyName("instance_id")]
        public string InstanceId { get; set; }

        [JsonPropertyName("availability_zone")]
        public string AvailabilityZone { get; set; }

        [JsonPropertyName("instance_size")]
        public string InstanceSize { get; set; }

        [JsonPropertyName("ami_id")]
        public string AmiId { get; set; }
    }
}