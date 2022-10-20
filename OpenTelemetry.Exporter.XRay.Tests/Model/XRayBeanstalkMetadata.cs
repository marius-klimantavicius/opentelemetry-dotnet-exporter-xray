using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayBeanstalkMetadata
    {
        [JsonPropertyName("environment_name")]
        public string Environment { get; set; }

        [JsonPropertyName("version_label")]
        public string VersionLabel { get; set; }

        [JsonPropertyName("deployment_id")]
        public long DeploymentId { get; set; }
    }
}