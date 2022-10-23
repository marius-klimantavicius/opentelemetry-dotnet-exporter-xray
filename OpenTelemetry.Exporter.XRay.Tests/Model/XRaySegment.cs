using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal class XRaySegment
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("start_time")]
        public double StartTime { get; set; }

        [JsonPropertyName("service")]
        public XRayServiceData Service { get; set; }

        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        [JsonPropertyName("user")]
        public string User { get; set; }

        [JsonPropertyName("resource_arn")]
        public string ResourceArn { get; set; }

        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; }

        [JsonPropertyName("end_time")]
        public double EndTime { get; set; }

        [JsonPropertyName("in_progress")]
        public bool InProgress { get; set; }

        [JsonPropertyName("http")]
        public XRayHttpData Http { get; set; }

        [JsonPropertyName("fault")]
        public bool IsFault { get; set; }

        [JsonPropertyName("error")]
        public bool IsError { get; set; }

        [JsonPropertyName("throttle")]
        public bool IsThrottle { get; set; }

        [JsonPropertyName("cause")]
        public XRayCauseData Cause { get; set; }

        [JsonPropertyName("aws")]
        public XRayAwsData Aws { get; set; }

        [JsonPropertyName("namespace")]
        public string Namespace { get; set; }

        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("precursor_ids")]
        public string PrecursorIds { get; set; }

        [JsonPropertyName("traced")]
        public bool Traced { get; set; }

        [JsonPropertyName("sql")]
        public XRaySqlData Sql { get; set; }

        [JsonPropertyName("annotations")]
        public IDictionary<string, object> Annotations { get; set; }

        [JsonPropertyName("metadata")]
        public IDictionary<string, IDictionary<string, object>> Metadata { get; set; }

        [JsonPropertyName("subsegments")]
        public List<XRaySegment> Subsegments { get; set; }
    }
}