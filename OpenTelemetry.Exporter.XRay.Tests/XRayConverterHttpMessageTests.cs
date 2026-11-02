using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public partial class XRayConverterHttpTests
    {
        [Theory]
        [InlineData(0, false, false, 12452)]
        [InlineData(0, true, true, 12452)]
        [InlineData(1, false, false, 4096)]
        [InlineData(1, false, true, 4096)]
        [InlineData(1, true, false, 4096)]
        [InlineData(1, true, true, 4096)]
        [InlineData(2, false, false, 4096)]
        [InlineData(2, true, true, 4096)]
        public void Should_extract_received_message_size_from_span_or_event(int mode, bool spanAttributes, bool reverse, long expected)
        {
            var tags = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>(XRayConventions.AttributeMessageType, "RECEIVED"),
                new KeyValuePair<string, object>(XRayConventions.AttributeMessagingPayloadSize, 12452L),
                new KeyValuePair<string, object>(XRayConventions.AttributeMessagingMessageBodySize, 4096L),
            };

            if (reverse)
                tags.Reverse();

            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
            });

            if (spanAttributes)
            {
                foreach (var (key, value) in tags)
                    activity.SetTag(key, value);
            }
            else
            {
                activity.AddEvent(new ActivityEvent("message", tags: new ActivityTagsCollection(tags)));
            }

            Assert.Equal(expected, ConvertSegment(CreateHttpConventionConverter(mode), activity).Http.Response.ContentLength);
        }

        [Theory]
        [InlineData(0, false, null)]
        [InlineData(0, true, 12452)]
        [InlineData(1, false, 4096)]
        [InlineData(1, true, 12452)]
        [InlineData(2, false, 4096)]
        [InlineData(2, true, null)]
        public void Should_gate_each_received_message_size_key(int mode, bool legacy, long? expected)
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
            });

            activity.AddEvent(new ActivityEvent("message", tags: new ActivityTagsCollection
            {
                [XRayConventions.AttributeMessageType] = "RECEIVED",
                [legacy ? XRayConventions.AttributeMessagingPayloadSize : XRayConventions.AttributeMessagingMessageBodySize] = legacy ? 12452L : 4096L,
            }));

            Assert.Equal(expected, ConvertSegment(CreateHttpConventionConverter(mode), activity).Http.Response.ContentLength);
        }

        [Theory]
        [InlineData("")]
        [InlineData("4096")]
        [InlineData(true)]
        public void Should_fall_back_from_wrong_type_modern_message_size(object invalid)
        {
            var tags = new Dictionary<string, object>
            {
                [XRayConventions.AttributeMessageType] = "RECEIVED",
                [XRayConventions.AttributeMessagingPayloadSize] = 12452L,
                [XRayConventions.AttributeMessagingMessageBodySize] = invalid,
            };
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
            });

            activity.AddEvent(new ActivityEvent("message", tags: new ActivityTagsCollection(tags)));
            Assert.Equal(12452, ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Response.ContentLength);

            foreach (var (key, value) in tags)
                activity.SetTag(key, value);

            Assert.Equal(12452, ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Response.ContentLength);
        }

        [Theory]
        [InlineData("SENT")]
        [InlineData("")]
        [InlineData(null)]
        public void Should_ignore_message_size_without_received_marker(string messageType)
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeMessagingMessageBodySize] = 4096L,
            });
            activity.SetTag(XRayConventions.AttributeMessageType, messageType);
            activity.AddEvent(new ActivityEvent("message", tags: new ActivityTagsCollection
            {
                [XRayConventions.AttributeMessageType] = messageType,
                [XRayConventions.AttributeMessagingMessageBodySize] = 4096L,
            }));
            Assert.Null(ConvertSegment(CreateHttpConventionConverter(1), activity).Http.Response.ContentLength);
        }

        [Fact]
        public void Should_preserve_explicit_response_length_and_zero_length_span_precedence()
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeMessageType] = "RECEIVED",
                [XRayConventions.AttributeMessagingMessageBodySize] = 0L,
            });
            activity.AddEvent(new ActivityEvent("message", tags: new ActivityTagsCollection
            {
                [XRayConventions.AttributeMessageType] = "RECEIVED",
                [XRayConventions.AttributeMessagingMessageBodySize] = 4096L,
            }));
            var converter = CreateHttpConventionConverter(1);
            Assert.Equal(0, ConvertSegment(converter, activity).Http.Response.ContentLength);

            activity.SetTag(XRayConventions.AttributeHttpResponseContentLength, 123L);
            Assert.Equal(123, ConvertSegment(converter, activity).Http.Response.ContentLength);
        }

        [Fact]
        public void Should_keep_disabled_message_attributes_in_metadata_and_clear_cached_values()
        {
            using var activity = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
                [XRayConventions.AttributeMessageType] = "RECEIVED",
                [XRayConventions.AttributeMessagingPayloadSize] = 12452L,
                [XRayConventions.AttributeMessagingMessageBodySize] = 4096L,
            });
            using var modern = JsonDocument.Parse(CreateHttpConventionConverter(2, false).Convert(Resource.Empty, activity));
            Assert.Equal(12452, modern.RootElement.GetProperty("metadata").GetProperty("default").GetProperty(XRayConventions.AttributeMessagingPayloadSize).GetInt64());
            using var legacy = JsonDocument.Parse(CreateHttpConventionConverter(0, false).Convert(Resource.Empty, activity));
            Assert.Equal(4096, legacy.RootElement.GetProperty("metadata").GetProperty("default").GetProperty(XRayConventions.AttributeMessagingMessageBodySize).GetInt64());

            using var next = ConstructHttpClientSpan(new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpRequestMethod] = "GET",
            });
            Assert.Null(ConvertSegment(CreateHttpConventionConverter(1), next).Http.Response.ContentLength);
        }
    }
}