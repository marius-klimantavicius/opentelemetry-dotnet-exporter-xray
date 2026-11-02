using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public partial class XRayConverterHttpTests
    {
        [Theory]
        [InlineData(false, "/users/junit", "version=test", "/users/junit?version=test")]
        [InlineData(false, "/users/junit", "?version=test", "/users/junit?version=test")]
        [InlineData(false, null, "version=test", "/?version=test")]
        [InlineData(false, "/users/junit", null, "/users/junit")]
        [InlineData(false, null, null, "/")]
        [InlineData(false, "", "version=test", "?version=test")]
        [InlineData(false, "/path", "", "/path?")]
        [InlineData(true, "/users/junit", "version=test", "/users/junit?version=test")]
        [InlineData(true, "/users/junit", "?version=test", "/users/junit?version=test")]
        [InlineData(true, null, "version=test", "/?version=test")]
        [InlineData(true, "/users/junit", null, "/users/junit")]
        [InlineData(true, null, null, "/")]
        [InlineData(true, "", "version=test", "?version=test")]
        [InlineData(true, "/path", "", "/path?")]
        public void Should_construct_url_with_stable_path_and_query_in_all_modes(bool server, string path, string query, string target)
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlScheme] = "https",
                [XRayConventions.AttributeServerAddress] = "api.example.com",
            };
            if (path != null)
                tags[XRayConventions.AttributeUrlPath] = path;
            if (query != null)
                tags[XRayConventions.AttributeUrlQuery] = query;

            using var activity = server ? ConstructHttpServerSpan(tags) : ConstructHttpClientSpan(tags);
            foreach (var mode in new[] { 0, 1, 2 })
            {
                var segment = ConvertSegment(CreateHttpConventionConverter(mode), activity);
                Assert.Equal("https://api.example.com" + target, segment.Http.Request.Url);
            }
        }

        [Fact]
        public void Should_construct_client_url_from_peer_name_without_peer_ip()
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeNetPeerName] = "api.example.com",
            });

            Assert.Equal("http://api.example.com/", ConvertDefault(activity).Http.Request.Url);
        }

        [Fact]
        public void Should_require_http_information_in_addition_to_peer_name()
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeNetPeerName] = "api.example.com",
            });

            Assert.Null(ConvertDefault(activity).Http);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Should_prefer_full_url_then_legacy_target_then_stable_path_and_query(bool server)
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeServerAddress] = "api.example.com",
                [XRayConventions.AttributeUrlPath] = "/path",
                [XRayConventions.AttributeUrlQuery] = "query=value",
                [XRayConventions.AttributeHttpTarget] = "/target?legacy=value",
                [XRayConventions.AttributeHttpUrl] = "https://legacy.example.com/full",
                [XRayConventions.AttributeUrlFull] = "https://modern.example.com/full",
            };
            using (var activity = server ? ConstructHttpServerSpan(tags) : ConstructHttpClientSpan(tags))
            {
                Assert.Equal("https://legacy.example.com/full", ConvertSegment(CreateHttpConventionConverter(0), activity).Http.Request.Url);
                Assert.Equal("https://modern.example.com/full", ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Request.Url);
                Assert.Equal("https://modern.example.com/full", ConvertSegment(CreateHttpConventionConverter(2), activity).Http.Request.Url);
            }

            tags.Remove(XRayConventions.AttributeHttpUrl);
            tags.Remove(XRayConventions.AttributeUrlFull);
            using (var activity = server ? ConstructHttpServerSpan(tags) : ConstructHttpClientSpan(tags))
            {
                Assert.Equal("http://api.example.com/target?legacy=value", ConvertSegment(CreateHttpConventionConverter(0), activity).Http.Request.Url);
                Assert.Equal("http://api.example.com/target?legacy=value", ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Request.Url);
                Assert.Equal("http://api.example.com/path?query=value", ConvertSegment(CreateHttpConventionConverter(2), activity).Http.Request.Url);
            }
        }

        [Theory]
        [InlineData(0, "new.example.com", 9090, 8080, 7070, "http://old.example.com:8080/users/junit")]
        [InlineData(1, "new.example.com", 9090, 8080, 7070, "http://new.example.com:9090/users/junit")]
        [InlineData(2, "new.example.com", 9090, 8080, 7070, "http://new.example.com:9090/users/junit")]
        [InlineData(1, "new.example.com", null, 8080, 7070, "http://new.example.com:8080/users/junit")]
        [InlineData(1, null, 9090, 8080, null, "http://old.example.com:9090/users/junit")]
        [InlineData(1, "new.example.com", null, null, 7070, "http://new.example.com:7070/users/junit")]
        [InlineData(2, "new.example.com", null, 8080, 7070, "http://new.example.com:7070/users/junit")]
        [InlineData(0, "new.example.com", null, null, 7070, "http://old.example.com/users/junit")]
        [InlineData(1, "", "", 8080, 7070, "http://old.example.com:8080/users/junit")]
        [InlineData(1, true, true, 8080, 7070, "http://old.example.com:8080/users/junit")]
        [InlineData(1, "new.example.com", 80, null, null, "http://new.example.com/users/junit")]
        [InlineData(1, "new.example.com", "8081", null, null, "http://new.example.com:8081/users/junit")]
        public void Should_apply_client_server_address_and_port_precedence(int mode, object address, object serverPort, object peerPort, object networkPort, string expected)
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlPath] = "/users/junit",
                [XRayConventions.AttributeNetPeerName] = "old.example.com",
            };

            if (address != null)
                tags[XRayConventions.AttributeServerAddress] = address;

            if (serverPort != null)
                tags[XRayConventions.AttributeServerPort] = serverPort;

            if (peerPort != null)
                tags[XRayConventions.AttributeNetPeerPort] = peerPort;

            if (networkPort != null)
                tags[XRayConventions.AttributeNetworkPeerPort] = networkPort;

            using var activity = ConstructHttpClientSpan(tags);
            Assert.Equal(expected, ConvertSegment(CreateHttpConventionConverter(mode), activity).Http.Request.Url);
        }

        [Theory]
        [InlineData("http", 80, "http://api.example.com/")]
        [InlineData("https", 443, "https://api.example.com/")]
        [InlineData("https", 8443, "https://api.example.com:8443/")]
        [InlineData("http", "8080", "http://api.example.com:8080/")]
        public void Should_handle_stable_default_and_nondefault_ports(string scheme, object port, string expected)
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeUrlScheme] = scheme,
                [XRayConventions.AttributeServerAddress] = "api.example.com",
                [XRayConventions.AttributeServerPort] = port,
            };
            using var client = ConstructHttpClientSpan(tags);
            using var server = ConstructHttpServerSpan(tags);

            Assert.Equal(expected, ConvertSegment(CreateHttpConventionConverter(1), client).Http.Request.Url);

            foreach (var mode in new[] { 0, 1, 2 })
                Assert.Equal(expected, ConvertSegment(CreateHttpConventionConverter(mode), server).Http.Request.Url);
        }

        [Theory]
        [InlineData(0, "old.example.com", "http://old.example.com/users/junit")]
        [InlineData(1, "old.example.com", "http://new.example.com:9090/users/junit")]
        [InlineData(2, "old.example.com", "http://new.example.com:9090/users/junit")]
        public void Should_prefer_modern_client_address_over_http_host_when_enabled(int mode, string legacyHost, string expected)
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeHttpHost] = legacyHost,
                [XRayConventions.AttributeServerAddress] = "new.example.com",
                [XRayConventions.AttributeServerPort] = 9090,
                [XRayConventions.AttributeUrlPath] = "/users/junit",
            });

            Assert.Equal(expected, ConvertSegment(CreateHttpConventionConverter(mode), activity).Http.Request.Url);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Should_not_mark_legacy_peer_ip_as_forwarded(bool hasForwardedIp)
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeNetPeerIp] = "10.8.17.36",
            };

            if (hasForwardedIp)
                tags[XRayConventions.AttributeHttpClientIp] = "1.2.3.4";

            using var activity = ConstructHttpServerSpan(tags);
            foreach (var mode in new[] { 0, 1 })
            {
                var request = ConvertSegment(CreateHttpConventionConverter(mode), activity).Http.Request;
                Assert.Equal(hasForwardedIp ? "1.2.3.4" : "10.8.17.36", request.ClientIp);
                Assert.Null(request.XForwardedFor);
            }
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(2, true)]
        public void Should_select_stable_http_aliases_independent_of_tag_order(int mode, bool reverse)
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeHttpUserAgent] = "legacy-agent",
                [XRayConventions.AttributeUserAgentOriginal] = "modern-agent",
                [XRayConventions.AttributeHttpStatusCode] = 201,
                [XRayConventions.AttributeHttpResponseStatusCode] = 202,
                [XRayConventions.AttributeHttpScheme] = "http",
                [XRayConventions.AttributeUrlScheme] = "https",
                [XRayConventions.AttributeServerAddress] = "api.example.com",
            };
            using var activity = ConstructHttpClientSpan(reverse ? tags.Reverse() : tags);
            var segment = ConvertSegment(CreateHttpConventionConverter(mode), activity);
            Assert.Equal(mode == 0 ? "POST" : "GET", segment.Http.Request.Method);
            Assert.Equal(mode == 0 ? "legacy-agent" : "modern-agent", segment.Http.Request.UserAgent);
            Assert.Equal(mode == 0 ? 201 : 202, segment.Http.Response.Status);
            Assert.Equal(mode == 0 ? "http://api.example.com/" : "https://api.example.com/", segment.Http.Request.Url);
        }

        [Theory]
        [InlineData("")]
        [InlineData(true)]
        [InlineData(42)]
        public void Should_fall_back_from_empty_or_wrong_type_modern_http_aliases(object invalid)
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpRequestMethod] = invalid,
                [XRayConventions.AttributeHttpUserAgent] = "legacy-agent",
                [XRayConventions.AttributeUserAgentOriginal] = invalid,
                [XRayConventions.AttributeHttpUrl] = "https://legacy.example.com/",
                [XRayConventions.AttributeUrlFull] = invalid,
            });
            var segment = ConvertSegment(CreateHttpConventionConverter(1), activity);
            Assert.Equal("GET", segment.Http.Request.Method);
            Assert.Equal("legacy-agent", segment.Http.Request.UserAgent);
            Assert.Equal("https://legacy.example.com/", segment.Http.Request.Url);
        }

        [Theory]
        [InlineData("")]
        [InlineData("202")]
        [InlineData(true)]
        public void Should_fall_back_from_wrong_type_modern_status(object invalid)
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpStatusCode] = 201,
                [XRayConventions.AttributeHttpResponseStatusCode] = invalid,
            });
            Assert.Equal(201, ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Response.Status);
        }

        [Fact]
        public void Should_leave_disabled_http_keys_available_for_metadata()
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeHttpHost] = "old.example.com",
                [XRayConventions.AttributeNetPeerPort] = 8080,
                [XRayConventions.AttributeNetworkPeerPort] = 9090,
                [XRayConventions.AttributeServerAddress] = "api.example.com",
            });
            using var legacy = JsonDocument.Parse(CreateHttpConventionConverter(0, false).Convert(Resource.Empty, activity));
            Assert.Equal(9090, legacy.RootElement.GetProperty("metadata").GetProperty("default").GetProperty("network.peer.port").GetInt32());
            using var modern = JsonDocument.Parse(CreateHttpConventionConverter(2, false).Convert(Resource.Empty, activity));
            var metadata = modern.RootElement.GetProperty("metadata").GetProperty("default");
            Assert.Equal("POST", metadata.GetProperty("http.method").GetString());
            Assert.Equal("old.example.com", metadata.GetProperty("http.host").GetString());
            Assert.Equal(8080, metadata.GetProperty("net.peer.port").GetInt32());
            Assert.False(metadata.TryGetProperty("http.request.method", out _));
            Assert.False(metadata.TryGetProperty("network.peer.port", out _));
        }

        [Fact]
        public void Should_ignore_all_legacy_http_network_keys_in_modern_only_mode()
        {
            using var activity = ConstructHttpServerSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpClientIp] = "1.2.3.4",
                [XRayConventions.AttributeHttpUserAgent] = "legacy-agent",
                [XRayConventions.AttributeHttpStatusCode] = 200,
                [XRayConventions.AttributeHttpUrl] = "https://legacy.example.com/",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeHttpHost] = "legacy.example.com",
                [XRayConventions.AttributeHttpTarget] = "/target",
                [XRayConventions.AttributeHttpServerName] = "server.example.com",
                [XRayConventions.AttributeNetHostName] = "host.example.com",
                [XRayConventions.AttributeNetHostPort] = 443,
                [XRayConventions.AttributeNetPeerName] = "peer.example.com",
                [XRayConventions.AttributeNetPeerPort] = 8080,
                [XRayConventions.AttributeNetPeerIp] = "10.8.17.36",
            });
            Assert.NotNull(ConvertSegment(CreateHttpConventionConverter(0), activity).Http);
            Assert.NotNull(ConvertSegment(CreateHttpConventionConverter(1), activity).Http);
            Assert.Null(ConvertSegment(CreateHttpConventionConverter(2), activity).Http);
        }

        [Theory]
        [InlineData(0, null)]
        [InlineData(1, null)]
        [InlineData(2, true)]
        public void Should_ignore_legacy_peer_evidence_for_forwarded_detection_only_when_disabled(int mode, bool? forwarded)
        {
            using var activity = ConstructHttpServerSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeClientAddress] = "1.2.3.4",
                [XRayConventions.AttributeNetPeerIp] = "10.8.17.36",
            });
            var request = ConvertSegment(CreateHttpConventionConverter(mode), activity).Http.Request;
            Assert.Equal("1.2.3.4", request.ClientIp);
            Assert.Equal(forwarded, request.XForwardedFor);
        }

        [Fact]
        public void Should_keep_upstream_server_host_and_port_precedence_in_additive_mode()
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeHttpHost] = "host.example.com:1234",
                [XRayConventions.AttributeHttpServerName] = "server.example.com",
                [XRayConventions.AttributeNetHostName] = "net.example.com",
                [XRayConventions.AttributeHostName] = "resource.example.com",
                [XRayConventions.AttributeServerAddress] = "modern.example.com",
                [XRayConventions.AttributeNetHostPort] = 8080,
                [XRayConventions.AttributeServerPort] = 9090,
            };

            foreach (var mode in new[] { 0, 1 })
            {
                using (var activity = ConstructHttpServerSpan(tags))
                    Assert.Equal("http://host.example.com:1234/", ConvertSegment(CreateHttpConventionConverter(mode), activity).Http.Request.Url);
            }

            tags.Remove(XRayConventions.AttributeHttpHost);
            using (var activity = ConstructHttpServerSpan(tags))
                Assert.Equal("http://server.example.com:8080/", ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Request.Url);

            tags.Remove(XRayConventions.AttributeHttpServerName);
            using (var activity = ConstructHttpServerSpan(tags))
                Assert.Equal("http://net.example.com:8080/", ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Request.Url);

            tags.Remove(XRayConventions.AttributeNetHostName);
            using (var activity = ConstructHttpServerSpan(tags))
                Assert.Equal("http://resource.example.com:8080/", ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Request.Url);

            tags.Remove(XRayConventions.AttributeHostName);
            using (var activity = ConstructHttpServerSpan(tags))
            {
                Assert.Equal("http://modern.example.com:8080/", ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Request.Url);
                Assert.Equal("http://modern.example.com:9090/", ConvertSegment(CreateHttpConventionConverter(2), activity).Http.Request.Url);
            }
        }

        [Fact]
        public void Should_not_use_network_port_for_http_host_with_embedded_port()
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeHttpHost] = "api.example.com:1234",
                [XRayConventions.AttributeServerPort] = 9090,
                [XRayConventions.AttributeNetworkPeerPort] = 8080,
            });
            Assert.Equal("http://api.example.com:1234/", ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Request.Url);
        }

        private static XRayConverter CreateHttpConventionConverter(int mode, bool indexAllAttributes = true)
        {
            return new XRayConverter(null, null, indexAllAttributes, false, emitV1HttpNetworkConventions: mode != 0, dontEmitV0HttpNetworkConventions: mode == 2);
        }
    }
}