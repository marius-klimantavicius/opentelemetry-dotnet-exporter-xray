using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Exporter.XRay.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayConverterConventionIntegrationTests : XRayTest
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("", true)]
        [InlineData(123, false)]
        [InlineData(123, true)]
        public void Empty_or_wrong_type_database_namespace_does_not_create_a_host_only_database_name(object dbNamespace, bool dontEmit)
        {
            using var span = ConstructSpan(ActivityKind.Client, new Dictionary<string, object>
            {
                ["db.namespace"] = dbNamespace,
                ["db.name"] = "legacy",
                ["db.connection_string"] = "jdbc:postgresql://db.host:5432",
                ["server.address"] = "modern.host",
            });

            var converter = new XRayConverter(null, null, false, false, emitV1DbConventions: true, dontEmitV0DbConventions: dontEmit);
            Assert.Equal(dontEmit ? span.DisplayName : "legacy@db.host", ConvertSegment(converter, span).Name);
        }

        [Theory]
        [InlineData(false, false, 429, 503, 429)]
        [InlineData(true, false, 429, 503, 503)]
        [InlineData(true, true, 429, 503, 503)]
        [InlineData(false, false, 503, 429, 503)]
        [InlineData(true, false, 503, 429, 429)]
        [InlineData(true, true, 503, 429, 429)]
        [InlineData(false, false, 429, null, 429)]
        [InlineData(true, false, 429, null, 429)]
        [InlineData(true, true, 429, null, null)]
        [InlineData(true, false, 429, "503", 429)]
        public void Http_response_and_cause_use_the_same_convention_precedence(bool emit, bool dontEmit, int legacy, object modern, int? expected)
        {
            foreach (var reverse in new[] { false, true })
            {
                var tags = new Dictionary<string, object>();
                if (reverse && modern != null)
                    tags["http.response.status_code"] = modern;

                tags["http.status_code"] = legacy;
                if (!reverse && modern != null)
                    tags["http.response.status_code"] = modern;

                tags["http.request.method"] = "GET";
                using var span = ConstructSpan(ActivityKind.Client, tags);
                var converter = new XRayConverter(null, null, false, false, emitV1HttpNetworkConventions: emit, dontEmitV0HttpNetworkConventions: dontEmit);
                var segment = ConvertSegment(converter, span);
                Assert.Equal(expected, segment.Http.Response.Status);
                Assert.Equal(expected >= 400 && expected <= 499, segment.IsError);
                Assert.Equal(expected == 429, segment.IsThrottle);
                Assert.Equal(expected >= 500 && expected <= 599, segment.IsFault);
                if (dontEmit)
                    Assert.Equal(legacy.ToString(), segment.Metadata["default"]["http.status_code"].ToString());
            }
        }
    }
}