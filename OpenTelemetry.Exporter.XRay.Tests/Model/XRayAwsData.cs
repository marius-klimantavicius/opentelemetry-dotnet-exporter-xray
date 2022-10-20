using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRayAwsData
    {
        [JsonPropertyName("elastic_beanstalk")]
        public XRayBeanstalkMetadata Beanstalk { get; set; }

        [JsonPropertyName("cloudwatch_logs")]
        public List<XRayLogGroupMetadata> CloudWatchLogs { get; set; }

        [JsonPropertyName("ecs")]
        public XRayECSMetadata Ecs { get; set; }

        [JsonPropertyName("ec2")]
        public XRayEC2Metadata Ec2 { get; set; }

        [JsonPropertyName("eks")]
        public XRayEKSMetadata Eks { get; set; }

        [JsonPropertyName("xray")]
        public XRayMetaData XRay { get; set; }

        [JsonPropertyName("account_id")]
        public string AccountId { get; set; }

        [JsonPropertyName("operation")]
        public string Operation { get; set; }

        [JsonPropertyName("region")]
        public string RemoteRegion { get; set; }

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; }

        [JsonPropertyName("queue_url")]
        public string QueueUrl { get; set; }

        [JsonPropertyName("table_name")]
        public string TableName { get; set; }

        [JsonPropertyName("retries")]
        public string Retries { get; set; }
    }
}