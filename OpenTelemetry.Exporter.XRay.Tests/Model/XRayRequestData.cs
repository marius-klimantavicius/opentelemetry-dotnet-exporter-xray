using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.XRay.Tests.Model
{
    internal struct XRayRequestData
    {
        [JsonPropertyName("x_forwarded_for")]
        public bool? XForwardedFor { get; set; }
        
        [JsonPropertyName("method")]
        public string Method { get; set; }
        
        [JsonPropertyName("url")]
        public string Url { get; set; }
        
        [JsonPropertyName("user_agent")]
        public string UserAgent { get; set; }
        
        [JsonPropertyName("client_ip")]
        public string ClientIp { get; set; }
    }
}