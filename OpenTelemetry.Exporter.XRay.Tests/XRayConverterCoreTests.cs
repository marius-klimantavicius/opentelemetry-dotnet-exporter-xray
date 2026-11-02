using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayConverterCoreTests : XRayTest
    {
        [Theory]
        [InlineData(ActivityKind.Consumer, true, null, null)]
        [InlineData(ActivityKind.Consumer, false, null, null)]
        [InlineData(ActivityKind.Producer, true, "subsegment", "remote")]
        [InlineData(ActivityKind.Producer, false, null, "remote")]
        public void Messaging_topology_preserves_consumer_resource(ActivityKind kind, bool hasParent, string type, string ns)
        {
            using var span = ConstructSpan(kind, hasParent: hasParent);
            var resource = new Resource(new Dictionary<string, object> { ["service.name"] = "messaging-service" });
            var segment = ConvertDefault(span, resource);
            Assert.Equal(type, segment.Type);
            Assert.Equal(ns, segment.Namespace);
            Assert.Equal(kind == ActivityKind.Consumer || !hasParent, segment.Annotations != null && segment.Annotations.ContainsKey("otel_resource_service_name"));
        }

        [Fact]
        public void Producer_preserves_AWS_namespace()
        {
            using var span = ConstructSpan(ActivityKind.Producer, new Dictionary<string, object> { ["rpc.system"] = "aws-api" });
            Assert.Equal("aws", ConvertDefault(span).Namespace);
        }

        [Theory]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData("true", false, true)]
        [InlineData(1, false, true)]
        [InlineData(null, false, true)]
        public void Inprogress_only_consumes_boolean_and_omits_end_time_when_true(object marker, bool expectedProgress, bool expectedEndTime)
        {
            using var span = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["aws.xray.inprogress"] = marker });
            var converter = new XRayConverter(null, null, false, false);

            using var doc = JsonDocument.Parse(converter.Convert(Resource.Empty, span));
            Assert.Equal(expectedProgress, doc.RootElement.TryGetProperty("in_progress", out _));
            Assert.Equal(expectedEndTime, doc.RootElement.TryGetProperty("end_time", out var endTime));

            if (expectedEndTime)
                Assert.Equal((span.StartTimeUtc - DateTime.UnixEpoch).TotalSeconds + span.Duration.TotalSeconds, endTime.GetDouble());
            if (marker is bool || marker == null)
                Assert.False(doc.RootElement.TryGetProperty("metadata", out _));
            else
                Assert.True(doc.RootElement.GetProperty("metadata").GetProperty("default").TryGetProperty("aws.xray.inprogress", out _));

            Assert.Equal(marker, span.GetTagItem("aws.xray.inprogress"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Annotation_selector_indexes_span_and_prefixed_resource_keys_and_consumes_selection(bool indexAll)
        {
            var attrs = new Dictionary<string, object>
            {
                ["aws.xray.annotations"] = new object[] { "attr1@1", "otel.resource.service.name", 123, true },
                ["attr1@1"] = "val1", ["attr2@2"] = "val2",
            };

            using var span = ConstructSpan(ActivityKind.Server, attrs);
            var resource = new Resource(new Dictionary<string, object> { ["service.name"] = "service" });
            var segment = ConvertSegment(new XRayConverter(null, null, indexAll, false), span, resource);
            Assert.Equal("val1", segment.Annotations["attr1_1"].ToString());
            Assert.Equal("service", segment.Annotations["otel_resource_service_name"].ToString());
            Assert.False(segment.Annotations.ContainsKey("aws_xray_annotations"));

            if (!indexAll)
            {
                Assert.Equal("val2", segment.Metadata["default"]["attr2@2"].ToString());
                Assert.False(segment.Metadata["default"].ContainsKey("aws.xray.annotations"));
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Explicit_indexing_restores_consumed_fields_including_with_index_all_and_allow_dot(bool indexAll, bool allowDot)
        {
            using var span = ConstructSpan(ActivityKind.Server, new Dictionary<string, object>
            {
                ["http.method"] = "POST", ["aws.operation"] = "aws_operation_val", ["rpc.method"] = "rpc_method_val",
            });
            var converter = new XRayConverter(null, new[] { "http.method", "aws.operation", "rpc.method" }, indexAll, false, allowDotInAnnotationKey: allowDot);
            var segment = ConvertSegment(converter, span);

            Assert.Equal("POST", segment.Http.Request.Method);
            Assert.Equal("aws_operation_val", segment.Aws.Operation);
            Assert.Equal("POST", segment.Annotations[allowDot ? "http.method" : "http_method"].ToString());
            Assert.Equal("aws_operation_val", segment.Annotations[allowDot ? "aws.operation" : "aws_operation"].ToString());
            Assert.Equal("rpc_method_val", segment.Annotations[allowDot ? "rpc.method" : "rpc_method"].ToString());
        }

        [Fact]
        public void Index_all_and_callback_do_not_restore_consumed_fields_without_explicit_selection()
        {
            using var span = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["http.method"] = "POST", ["aws.operation"] = "op" });
            Assert.Null(ConvertDefault(span).Annotations);
            Assert.Null(ConvertSegment(new XRayConverter((key, resource) => true, null, false, false), span).Annotations);
        }

        [Fact]
        public void Selection_and_explicit_keys_take_precedence_over_callback_preserving_resource_flag()
        {
            var calls = new List<(string key, bool resource)>();
            using var span = ConstructSpan(ActivityKind.Server, new Dictionary<string, object>
            {
                ["aws.xray.annotations"] = new[] { "selected", "otel.resource.selected" },
                ["selected"] = "span-selected", ["explicit"] = "span-explicit", ["callback"] = "span-callback", ["metadata"] = "value",
            });

            var converter = new XRayConverter((key, resource) =>
            {
                calls.Add((key, resource));
                return key == "callback";
            }, new[] { "explicit", "otel.resource.explicit" }, false, false);
            var resource = new Resource(new Dictionary<string, object> { ["selected"] = "resource-selected", ["explicit"] = "resource-explicit", ["callback"] = "resource-callback" });
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal(6, segment.Annotations.Count);
            Assert.Equal("value", segment.Metadata["default"]["metadata"].ToString());
            Assert.DoesNotContain(calls, call => call.key == "selected" || call.key == "explicit");
            Assert.Contains(("callback", false), calls);
            Assert.Contains(("callback", true), calls);
        }

        [Theory]
        [InlineData("{\"custom_key\":\"custom_value\"}", true)]
        [InlineData("retain-value", false)]
        [InlineData("[]", false)]
        [InlineData("null", false)]
        [InlineData("42", false)]
        public void Namespaced_metadata_parses_objects_merges_default_and_preserves_invalid_input(string json, bool valid)
        {
            using var span = ConstructSpan(ActivityKind.Server, new Dictionary<string, object>
            {
                ["attr1@1"] = "val1",
                ["aws.xray.metadata.DeFaUlT"] = json,
                ["aws.xray.metadata.http"] = "{\"connection\":{\"reused\":false,\"was_idle\":false}}",
                ["aws.xray.metadata.non-xray-sdk"] = "retain-value",
            });
            var resource = new Resource(new Dictionary<string, object> { ["service.name"] = "service" });
            var segment = ConvertSegment(new XRayConverter(null, null, false, false), span, resource);
            Assert.Equal(2, segment.Metadata.Count);
            var metadata = segment.Metadata["default"];
            Assert.Equal("val1", metadata["attr1@1"].ToString());
            Assert.Equal("service", metadata["otel.resource.service.name"].ToString());
            Assert.Equal("retain-value", metadata["aws.xray.metadata.non-xray-sdk"].ToString());
            Assert.Equal(!valid, metadata.ContainsKey("aws.xray.metadata.DeFaUlT"));

            if (valid)
                Assert.Equal("custom_value", metadata["custom_key"].ToString());

            var connection = (JsonElement)segment.Metadata["http"]["connection"];
            Assert.False(connection.GetProperty("reused").GetBoolean());
            Assert.False(connection.GetProperty("was_idle").GetBoolean());
        }

        [Fact]
        public void Indexed_metadata_string_remains_annotation_and_complex_resource_values_remain_metadata()
        {
            using var span = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["aws.xray.metadata.custom"] = "{\"key\":1}" });
            var resource = new Resource(new Dictionary<string, object> { ["array"] = new[] { "one", "two" } });
            var segment = ConvertDefault(span, resource);
            Assert.Equal("{\"key\":1}", segment.Annotations["aws_xray_metadata_custom"].ToString());
            Assert.Single(segment.Metadata);
            Assert.True(segment.Metadata["default"].ContainsKey("otel.resource.array"));
        }

        [Theory]
        [InlineData("<subDomain>.example.com,1413", "subDomain.example.com1413")]
        [InlineData(",<>", "span")]
        [InlineData("EP @ test_15.testing-dömain.org#GO", "EP @ test_15.testing-dömain.org#GO")]
        [InlineData("foo\\bar", "foo\\bar")]
        public void Segment_name_filters_commas_and_preserves_existing_allowed_characters(string name, string expected)
        {
            Assert.Equal(expected, XRayConverter.FixSegmentName(name));
        }

        [Theory]
        [InlineData("jdbc:mysql://db.dev.example.com:3306", "db@db.dev.example.com")]
        [InlineData("mysql://db.dev.example.com:3306", "db@db.dev.example.com")]
        [InlineData("invalid", "db")]
        public void Legacy_database_name_strips_JDBC_prefix_before_hostname_parsing(string connection, string expected)
        {
            using var span = ConstructSpan(ActivityKind.Client, new Dictionary<string, object> { ["db.name"] = "db", ["db.connection_string"] = connection });
            Assert.Equal(expected, ConvertDefault(span).Name);
        }

        [Theory]
        [InlineData(false, false, "legacy@legacy.example.com")]
        [InlineData(true, false, "modern@modern.example.com")]
        [InlineData(true, true, "modern@modern.example.com")]
        public void Database_name_gate_prefers_modern_keys_when_enabled(bool emit, bool dontEmit, string expected)
        {
            using var span = ConstructSpan(ActivityKind.Client, new Dictionary<string, object>
            {
                ["db.name"] = "legacy", ["db.connection_string"] = "jdbc:mysql://legacy.example.com:3306",
                ["db.namespace"] = "modern", ["server.address"] = "modern.example.com",
            });
            var converter = new XRayConverter(null, null, false, false, emitV1DbConventions: emit, dontEmitV0DbConventions: dontEmit);
            Assert.Equal(expected, ConvertSegment(converter, span).Name);
        }

        [Theory]
        [InlineData(false, false, "legacy-host")]
        [InlineData(true, false, "legacy-host")]
        [InlineData(true, true, "modern-host")]
        public void Network_name_gate_retains_upstream_legacy_host_precedence(bool emit, bool dontEmit, string expected)
        {
            using var span = ConstructSpan(ActivityKind.Client, new Dictionary<string, object> { ["http.host"] = "legacy-host", ["net.peer.name"] = "peer", ["server.address"] = "modern-host" });
            var converter = new XRayConverter(null, null, false, false, emitV1HttpNetworkConventions: emit, dontEmitV0HttpNetworkConventions: dontEmit);
            Assert.Equal(expected, ConvertSegment(converter, span).Name);
        }

        [Theory]
        [InlineData(false, false, "destination operation")]
        [InlineData(true, false, "modern@modern-host")]
        [InlineData(true, true, "modern@modern-host")]
        public void New_only_database_name_requires_emit_gate(bool emit, bool dontEmit, string expected)
        {
            using var span = ConstructSpan(ActivityKind.Client, new Dictionary<string, object> { ["db.namespace"] = "modern", ["server.address"] = "modern-host" });
            var converter = new XRayConverter(null, null, false, false, emitV1DbConventions: emit, dontEmitV0DbConventions: dontEmit);
            Assert.Equal(expected, ConvertSegment(converter, span).Name);
        }

        [Theory]
        [InlineData(false, false, "peer")]
        [InlineData(true, false, "modern-host")]
        [InlineData(true, true, "modern-host")]
        public void Modern_network_name_precedes_peer_name_only_with_emit_gate(bool emit, bool dontEmit, string expected)
        {
            using var span = ConstructSpan(ActivityKind.Client, new Dictionary<string, object> { ["server.address"] = "modern-host", ["net.peer.name"] = "peer" });
            var converter = new XRayConverter(null, null, false, false, emitV1HttpNetworkConventions: emit, dontEmitV0HttpNetworkConventions: dontEmit);
            Assert.Equal(expected, ConvertSegment(converter, span).Name);
        }

        [Fact]
        public void Wrong_type_annotation_selection_is_preserved_as_metadata()
        {
            using var span = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["aws.xray.annotations"] = "not a list" });
            var segment = ConvertSegment(new XRayConverter(null, null, false, false), span);
            Assert.Equal("not a list", segment.Metadata["default"]["aws.xray.annotations"].ToString());
        }

        [Fact]
        public void Consumed_boolean_inprogress_stays_consumed_when_explicitly_indexed()
        {
            using var span = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["aws.xray.inprogress"] = false });
            var converter = new XRayConverter(null, new[] { "aws.xray.inprogress" }, true, false);
            var segment = ConvertSegment(converter, span);
            Assert.Null(segment.Annotations);
            Assert.False(segment.InProgress);
        }

        [Fact]
        public void Callback_can_reenter_converter_without_corrupting_cached_writer_or_tags()
        {
            using var nestedSpan = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["nested"] = "value" });
            using var outerSpan = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["outer"] = "value" });
            var entered = false;
            var converter = default(XRayConverter);
            converter = new XRayConverter((key, resource) =>
            {
                if (!entered)
                {
                    entered = true;
                    // ReSharper disable once AccessToModifiedClosure
                    // ReSharper disable once AccessToDisposedClosure
                    var nested = ConvertSegment(converter, nestedSpan);
                    Assert.Equal("value", nested.Metadata["default"]["nested"].ToString());
                }

                return false;
            }, null, false, false);
            var outer = ConvertSegment(converter, outerSpan);
            Assert.Single(outer.Metadata["default"]);
            Assert.Equal("value", outer.Metadata["default"]["outer"].ToString());
            Assert.Equal(outerSpan.SpanId.ToHexString(), outer.Id);
        }

        [Fact]
        public void Cache_recovers_from_serialization_failure_and_does_not_leak_consumed_fields()
        {
            var converter = new XRayConverter(null, null, false, false);
            var cyclic = new Dictionary<string, object>();
            cyclic["self"] = cyclic;

            using var failing = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["bad"] = cyclic });
            Assert.Throws<JsonException>(() => converter.Convert(Resource.Empty, failing));

            using var first = ConstructSpan(ActivityKind.Server, new Dictionary<string, object> { ["http.method"] = "POST", ["first"] = "one" });
            using var second = ConstructSpan(ActivityKind.Consumer, new Dictionary<string, object> { ["second"] = "two" });
            Assert.Equal("POST", ConvertSegment(converter, first).Http.Request.Method);

            var segment = ConvertSegment(converter, second);
            Assert.Null(segment.Http);
            Assert.Single(segment.Metadata["default"]);
            Assert.Equal("two", segment.Metadata["default"]["second"].ToString());
        }
    }
}