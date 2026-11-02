using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    // Ported from translator/span_links_test.go, with .NET trace-ID policy regressions.
    public class XRayConverterLinkTests : XRayTest
    {
        [Fact]
        public void Simple_link_converts_ids_and_omits_empty_attributes()
        {
            var context = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var span = ConstructSpan(ActivityKind.Consumer, links: new[] { new ActivityLink(context) });
            var segment = ConvertDefault(span);
            var link = Assert.Single(segment.Links);
            Assert.Equal(context.SpanId.ToHexString(), link.Id);
            Assert.Equal(ToXRay(context.TraceId), link.TraceId);
            Assert.Null(link.Attributes);
            using var parsed = JsonDocument.Parse(CreateDefaultConverter().Convert(Resource.Empty, span));
            Assert.False(parsed.RootElement.GetProperty("links")[0].TryGetProperty("attributes", out _));
        }

        [Fact]
        public void No_links_omits_field()
        {
            using var span = ConstructSpan(ActivityKind.Server);
            Assert.Null(ConvertDefault(span).Links);
            using var parsed = JsonDocument.Parse(CreateDefaultConverter().Convert(Resource.Empty, span));
            Assert.False(parsed.RootElement.TryGetProperty("links", out _));
        }

        [Fact]
        public void Two_links_keep_order_ids_and_attributes()
        {
            var first = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var second = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var span = ConstructSpan(ActivityKind.Consumer, links: new[]
            {
                new ActivityLink(first, new ActivityTagsCollection { ["myKey1"] = "ABC" }),
                new ActivityLink(second, new ActivityTagsCollection { ["myKey2"] = 1234L }),
            });
            var links = ConvertDefault(span).Links;
            Assert.Equal(2, links.Count);
            Assert.Equal(ToXRay(first.TraceId), links[0].TraceId);
            Assert.Equal(first.SpanId.ToHexString(), links[0].Id);
            Assert.Equal("ABC", links[0].Attributes["myKey1"].ToString());
            Assert.Equal(ToXRay(second.TraceId), links[1].TraceId);
            Assert.Equal(second.SpanId.ToHexString(), links[1].Id);
            Assert.Equal(1234L, ((JsonElement)links[1].Attributes["myKey2"]).GetInt64());
        }

        [Fact]
        public void Link_complex_attributes_preserve_types_arrays_and_nested_objects()
        {
            var attributes = new ActivityTagsCollection
            {
                ["myKey1"] = "myValue", ["myKey2"] = true, ["myKey3"] = 112233L, ["myKey4"] = 3.1415,
                ["myKey5"] = new[] { "apple", "pear", "banana" },
                ["myKey6"] = new[] { true, false, false, true },
                ["myKey7"] = new long[] { 1234, 5678, 9012 },
                ["myKey8"] = new[] { 2.718, 1.618 },
                ["nested"] = new Dictionary<string, object> { ["value"] = "kept" },
            };
            var context = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var span = ConstructSpan(ActivityKind.Server, links: new[] { new ActivityLink(context, attributes) });
            var link = Assert.Single(ConvertDefault(span).Links);
            Assert.Equal(9, link.Attributes.Count);
            Assert.Equal("myValue", link.Attributes["myKey1"].ToString());
            Assert.True(((JsonElement)link.Attributes["myKey2"]).GetBoolean());
            Assert.Equal(112233L, ((JsonElement)link.Attributes["myKey3"]).GetInt64());
            Assert.Equal(3.1415, ((JsonElement)link.Attributes["myKey4"]).GetDouble());
            Assert.Equal("pear", ((JsonElement)link.Attributes["myKey5"])[1].GetString());
            Assert.False(((JsonElement)link.Attributes["myKey6"])[1].GetBoolean());
            Assert.Equal(9012, ((JsonElement)link.Attributes["myKey7"])[2].GetInt64());
            Assert.Equal(1.618, ((JsonElement)link.Attributes["myKey8"])[1].GetDouble());
            Assert.Equal("kept", ((JsonElement)link.Attributes["nested"]).GetProperty("value").GetString());
        }

        [Theory]
        [InlineData(-31, false)]
        [InlineData(1, false)]
        [InlineData(-31, true)]
        public void Invalid_link_rejects_entire_span_even_when_span_trace_rewriting_enabled(int days, bool overwrite)
        {
            var epoch = DateTimeOffset.UtcNow.AddDays(days).ToUnixTimeSeconds();
            var linkedTraceId = ActivityTraceId.CreateFromString((epoch.ToString("x8") + "aaaaaaaaaaaaaaaaaaaaaaaa").AsSpan());
            var context = new ActivityContext(linkedTraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var span = ConstructSpan(ActivityKind.Consumer, links: new[] { new ActivityLink(context) });
            var converter = new XRayConverter(null, null, false, false, validateTraceId: true, overwriteInvalidTraceId: overwrite);
            Assert.Null(converter.Convert(Resource.Empty, span));
            var (first, second) = converter.ConvertDocuments(Resource.Empty, span);
            Assert.Null(first);
            Assert.Null(second);
        }

        [Fact]
        public void Valid_link_is_accepted_with_timestamp_validation_enabled()
        {
            var context = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var span = ConstructSpan(ActivityKind.Consumer, links: new[] { new ActivityLink(context) });
            var converter = new XRayConverter(null, null, false, false, validateTraceId: true);
            Assert.Equal(ToXRay(context.TraceId), Assert.Single(ConvertSegment(converter, span).Links).TraceId);
        }

        [Fact]
        public void Invalid_link_rejects_local_root_atomically()
        {
            var context = new ActivityContext(ActivityTraceId.CreateFromString("00000001aaaaaaaaaaaaaaaaaaaaaaaa".AsSpan()), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var span = ConstructSpan(ActivityKind.Consumer,
                new Dictionary<string, object> { ["aws.span.kind"] = "LOCAL_ROOT" }, new[] { new ActivityLink(context) });
            var converter = new XRayConverter(null, null, false, false, validateTraceId: true);
            var (first, second) = converter.ConvertDocuments(Resource.Empty, span);
            Assert.Null(first);
            Assert.Null(second);
        }

        [Fact]
        public void Default_skips_timestamp_validation_for_links()
        {
            var linkedTraceId = ActivityTraceId.CreateFromString("00000001aaaaaaaaaaaaaaaaaaaaaaaa".AsSpan());
            var context = new ActivityContext(linkedTraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var span = ConstructSpan(ActivityKind.Consumer, links: new[] { new ActivityLink(context) });
            Assert.Equal(ToXRay(linkedTraceId), Assert.Single(ConvertDefault(span).Links).TraceId);
        }

        private static string ToXRay(ActivityTraceId id)
        {
            var hex = id.ToHexString();
            return string.Concat("1-", hex.AsSpan(0, 8), "-", hex.AsSpan(8));
        }
    }
}