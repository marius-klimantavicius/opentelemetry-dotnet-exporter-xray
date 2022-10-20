using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayECSMetadata
    {
        [JsonPropertyName("container")]
        public string ContainerName { get; set; }

        [JsonPropertyName("container_id")]
        public string ContainerId { get; set; }

        [JsonPropertyName("task_arn")]
        public string TaskArn { get; set; }

        [JsonPropertyName("task_family")]
        public string TaskFamily { get; set; }

        [JsonPropertyName("cluster_arn")]
        public string ClusterArn { get; set; }

        [JsonPropertyName("container_arn")]
        public string ContainerArn { get; set; }

        [JsonPropertyName("availability_zone")]
        public string AvailabilityZone { get; set; }

        [JsonPropertyName("launch_type")]
        public string LaunchType { get; set; }
    }
}