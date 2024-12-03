using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Exporter.XRay.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayConverterHttpTests : XRayTest
    {
        [Fact]
        public void Should_use_provided_url_client()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.com/users/junit",
                [XRayConventions.AttributeHttpStatusCode] = 200,
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_use_provided_url_client_stable()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlFull] = "https://api.example.com/users/junit",
                [XRayConventions.AttributeHttpResponseStatusCode] = 200,
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_http_attributes_client()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeHttpHost] = "api.example.com",
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
                [XRayConventions.AttributeHttpStatusCode] = 200,
                ["user.id"] = "junit",
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_peer_attributes_client()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "http",
                [XRayConventions.AttributeNetPeerName] = "kb234.example.com",
                [XRayConventions.AttributeNetPeerPort] = 8080,
                [XRayConventions.AttributeNetPeerIp] = "10.8.17.36",
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
                [XRayConventions.AttributeHttpStatusCode] = 200,
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("10.8.17.36", segment.Http.Request.ClientIp);
            Assert.Equal("http://kb234.example.com:8080/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_peer_attributes_client_stable()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlScheme] = "http",
                [XRayConventions.AttributeNetPeerName] = "kb234.example.com",
                [XRayConventions.AttributeNetPeerPort] = 8080,
                [XRayConventions.AttributeNetPeerIp] = "10.8.17.36",
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
                [XRayConventions.AttributeHttpStatusCode] = 200,
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("10.8.17.36", segment.Http.Request.ClientIp);
            Assert.Equal("http://kb234.example.com:8080/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_prefer_http_attributes_over_peer_client()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpClientIp] = "1.2.3.4",
                [XRayConventions.AttributeNetPeerIp] = "10.8.17.36",
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("1.2.3.4", segment.Http.Request.ClientIp);
        }

        [Fact]
        public void Should_prefer_http_attributes_over_peer_client_stable()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeUrlFull] = "https://api.example.com/users/junit",
                [XRayConventions.AttributeClientAddress] = "1.2.3.4",
                [XRayConventions.AttributeNetworkPeerAddress] = "10.8.17.36",
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("1.2.3.4", segment.Http.Request.ClientIp);
        }

        [Fact]
        public void Should_construct_url_from_ipv4_client()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "http",
                [XRayConventions.AttributeNetPeerIp] = "10.8.17.36",
                [XRayConventions.AttributeNetPeerPort] = "8080",
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("http://10.8.17.36:8080/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_ipv6_client()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeNetPeerIp] = "2001:db8:85a3::8a2e:370:7334",
                [XRayConventions.AttributeNetPeerPort] = "443",
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
            };

            var span = ConstructHttpClientSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://2001:db8:85a3::8a2e:370:7334/users/junit", segment.Http.Request.Url);
        }
        
        [Fact]
        public void Should_use_provided_url_server()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.com/users/junit",
                [XRayConventions.AttributeHttpClientIp] = "192.168.15.32",
                [XRayConventions.AttributeHttpUserAgent] = "PostmanRuntime/7.21.0",
                [XRayConventions.AttributeHttpStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }
        
        [Fact]
        public void Should_use_provided_url_server_stable()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlFull] = "https://api.example.com/users/junit",
                [XRayConventions.AttributeClientAddress] = "192.168.15.32",
                [XRayConventions.AttributeUserAgentOriginal] = "PostmanRuntime/7.21.0",
                [XRayConventions.AttributeHttpResponseStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_http_attributes_server()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeHttpHost] = "api.example.com",
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
                [XRayConventions.AttributeHttpClientIp] = "192.168.15.32",
                [XRayConventions.AttributeHttpStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_http_attributes_server_stable()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlScheme] = "https",
                [XRayConventions.AttributeServerAddress] = "api.example.com",
                [XRayConventions.AttributeUrlPath] = "/users/junit",
                [XRayConventions.AttributeClientAddress] = "192.168.15.32",
                [XRayConventions.AttributeHttpResponseStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_http_attributes_and_ignore_default_port_server()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeHttpServerName] = "api.example.com",
                [XRayConventions.AttributeNetHostPort] = 443,
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
                [XRayConventions.AttributeHttpClientIp] = "192.168.15.32",
                [XRayConventions.AttributeHttpStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_http_attributes_and_ignore_default_port_server_stable()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlScheme] = "https",
                [XRayConventions.AttributeServerAddress] = "api.example.com",
                [XRayConventions.AttributeServerPort] = 443,
                [XRayConventions.AttributeUrlPath] = "/users/junit",
                [XRayConventions.AttributeClientAddress] = "192.168.15.32",
                [XRayConventions.AttributeHttpResponseStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("https://api.example.com/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_http_attributes_and_port_server()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "http",
                [XRayConventions.AttributeHostName] = "kb234.example.com",
                [XRayConventions.AttributeNetHostPort] = 8080,
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
                [XRayConventions.AttributeHttpClientIp] = "192.168.15.32",
                [XRayConventions.AttributeHttpStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);
            ConstructTimedEventsWithReceivedMessageEvent(span);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("http://kb234.example.com:8080/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_construct_url_from_http_attributes_and_port_server_stable()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlScheme] = "http",
                [XRayConventions.AttributeServerAddress] = "kb234.example.com",
                [XRayConventions.AttributeServerPort] = 8080,
                [XRayConventions.AttributeUrlPath] = "/users/junit",
                [XRayConventions.AttributeClientAddress] = "192.168.15.32",
                [XRayConventions.AttributeHttpResponseStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);
            ConstructTimedEventsWithReceivedMessageEvent(span);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("http://kb234.example.com:8080/users/junit", segment.Http.Request.Url);
        }

        [Fact]
        public void Should_not_construct_url_if_missing_url_attributes()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "http",
                [XRayConventions.AttributeHttpClientIp] = "192.168.15.32",
                [XRayConventions.AttributeHttpUserAgent] = "PostmanRuntime/7.21.0",
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
                [XRayConventions.AttributeNetHostPort] = 443,
                [XRayConventions.AttributeNetPeerPort] = 8080,
                [XRayConventions.AttributeHttpStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);
            ConstructTimedEventsWithReceivedMessageEvent(span);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Null(segment.Http.Request.Url);
            Assert.Equal("192.168.15.32", segment.Http.Request.ClientIp);
            Assert.Equal("GET", segment.Http.Request.Method);
            Assert.Equal("PostmanRuntime/7.21.0", segment.Http.Request.UserAgent);

            Assert.Equal(12452, segment.Http.Response.ContentLength);
            Assert.Equal(200, segment.Http.Response.Status);
        }

        [Fact]
        public void Should_not_construct_url_if_missing_url_attributes_stable()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlScheme] = "http",
                [XRayConventions.AttributeClientAddress] = "192.168.15.32",
                [XRayConventions.AttributeUserAgentOriginal] = "PostmanRuntime/7.21.0",
                [XRayConventions.AttributeUrlPath] = "/users/junit",
                [XRayConventions.AttributeServerPort] = 443,
                [XRayConventions.AttributeHttpResponseStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);
            ConstructTimedEventsWithReceivedMessageEvent(span);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Null(segment.Http.Request.Url);
            Assert.Equal("192.168.15.32", segment.Http.Request.ClientIp);
            Assert.Equal("GET", segment.Http.Request.Method);
            Assert.Equal("PostmanRuntime/7.21.0", segment.Http.Request.UserAgent);

            Assert.Equal(12452, segment.Http.Response.ContentLength);
            Assert.Equal(200, segment.Http.Response.Status);
        }

        [Fact]
        public void Should_not_construct_url_if_missing_url_attributes_duplicated()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "http",
                [XRayConventions.AttributeUrlScheme] = "http",
                [XRayConventions.AttributeHttpClientIp] = "192.168.15.32",
                [XRayConventions.AttributeClientAddress] = "192.168.15.32",
                [XRayConventions.AttributeHttpUserAgent] = "PostmanRuntime/7.21.0",
                [XRayConventions.AttributeUserAgentOriginal] = "PostmanRuntime/7.21.0",
                [XRayConventions.AttributeHttpTarget] = "/users/junit",
                [XRayConventions.AttributeUrlPath] = "/users/junit",
                [XRayConventions.AttributeNetHostPort] = 443,
                [XRayConventions.AttributeServerPort] = 443,
                [XRayConventions.AttributeNetPeerPort] = 8080,
                [XRayConventions.AttributeHttpStatusCode] = 200,
                [XRayConventions.AttributeHttpResponseStatusCode] = 200,
            };

            var span = ConstructHttpServerSpan(attributes);
            ConstructTimedEventsWithReceivedMessageEvent(span);

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Null(segment.Http.Request.Url);
            Assert.Equal("192.168.15.32", segment.Http.Request.ClientIp);
            Assert.Equal("GET", segment.Http.Request.Method);
            Assert.Equal("PostmanRuntime/7.21.0", segment.Http.Request.UserAgent);

            Assert.Equal(12452, segment.Http.Response.ContentLength);
            Assert.Equal(200, segment.Http.Response.Status);
        }

        [Fact]
        public void Should_infer_x_forwarded_for_from_client_without_network_peer_address()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeUrlFull] = "https://api.example.com/users/junit",
                [XRayConventions.AttributeClientAddress] = "192.168.15.32",
            };

            var span = ConstructHttpServerSpan(attributes);
            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal(true, segment.Http.Request.XForwardedFor);
        }

        [Fact]
        public void Should_ignore_x_forwarded_from_client_and_network_peer_address()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeUrlFull] = "https://api.example.com/users/junit",
                [XRayConventions.AttributeClientAddress] = "192.168.15.32",
                [XRayConventions.AttributeNetworkPeerAddress] = "192.168.15.32",
            };

            var span = ConstructHttpServerSpan(attributes);
            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Equal("192.168.15.32", segment.Http.Request.ClientIp);
            Assert.Null(segment.Http.Request.XForwardedFor);
        }

        [Fact]
        public void Should_ignore_x_forwarded_and_client_ip_if_not_ip()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeUrlFull] = "https://api.example.com/users/junit",
                [XRayConventions.AttributeClientAddress] = "api.example.com",
                [XRayConventions.AttributeNetworkPeerAddress] = "api.example.com",
            };

            var span = ConstructHttpServerSpan(attributes);
            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Http);
            Assert.NotNull(segment.Http.Request);
            Assert.Null(segment.Http.Request.ClientIp);
            Assert.Null(segment.Http.Request.XForwardedFor);
        }

        private Activity ConstructHttpClientSpan(IEnumerable<KeyValuePair<string, object>> tags)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddSeconds(-90);

            var parentContext = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var activity = ActivitySource.CreateActivity("/users/test", ActivityKind.Client, parentContext, tags);
            Assert.NotNull(activity);

            activity.SetStartTime(startTime);
            activity.SetEndTime(endTime);
            activity.SetStatus(ActivityStatusCode.Unset, "OK");

            return activity;
        }

        private Activity ConstructHttpServerSpan(IEnumerable<KeyValuePair<string, object>> tags)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddSeconds(-90);

            var parentContext = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var activity = ActivitySource.CreateActivity("/users/test", ActivityKind.Server, parentContext, tags);
            Assert.NotNull(activity);

            activity.SetStartTime(startTime);
            activity.SetEndTime(endTime);
            activity.SetStatus(ActivityStatusCode.Unset, "OK");

            return activity;
        }
    }
}