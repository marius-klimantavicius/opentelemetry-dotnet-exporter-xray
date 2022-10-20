using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayEKSMetadata
    {
        [JsonPropertyName("cluster_name")]
        public string ClusterName { get; set; }

        [JsonPropertyName("pod")]
        public string Pod { get; set; }

        [JsonPropertyName("container_id")]
        public string ContainerId { get; set; }
    }
}