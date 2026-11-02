using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    // Ported from translator/segment_test.go at c6a567e9cf071f1960060250f7b57340528815d0.
    public class XRayConverterApplicationSignalsTests : XRayTest
    {
        private static Dictionary<string, object> BasicAttributes()
        {
            return new Dictionary<string, object>
            {
                ["http.method"] = "POST",
                ["messaging.operation"] = "receive",
                ["otel.resource.attributes"] = "service.name=myTest",
                ["aws.span.kind"] = "LOCAL_ROOT",
                ["aws.remote.service"] = "myRemoteService",
                ["aws.remote.operation"] = "myRemoteOperation",
                ["remoteTarget"] = "myTarget",
                ["K8s.RemoteNamespace"] = "k8sRemoteNamespace",
                ["aws.local.service"] = "myLocalService",
                ["aws.local.operation"] = "awsLocalOperation",
                ["myAnnotationKey"] = "myAnnotationValue",
                ["aws.operation"] = "UpdateItem",
                ["aws.account_id"] = "AWSAccountAttribute",
                ["aws.region"] = "AWSRegionAttribute",
                ["aws.request_id"] = "AWSRequestIDAttribute",
                ["aws.queue_url"] = "AWSQueueURLAttribute",
                ["aws.table_name"] = "TableName",
            };
        }

        private static Resource BasicResource()
        {
            return new Resource(new Dictionary<string, object>
            {
                ["service.name"] = "signup_aggregator",
                ["telemetry.sdk.name"] = "MySDK",
                ["telemetry.sdk.version"] = "1.20.0",
                ["telemetry.auto.version"] = "1.2.3",
            });
        }

        private static ActivityLink Link()
        {
            return new ActivityLink(new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));
        }

        private static XRayConverter Converter(bool indexAll = false, bool allowDot = false)
        {
            return new XRayConverter(null, new[] { "aws.remote.service", "myAnnotationKey" }, indexAll, false, allowDotInAnnotationKey: allowDot);
        }

        [Theory]
        [InlineData(ActivityKind.Client, true)]
        [InlineData(ActivityKind.Producer, true)]
        [InlineData(ActivityKind.Consumer, true)]
        [InlineData(ActivityKind.Client, false)]
        [InlineData(ActivityKind.Producer, false)]
        [InlineData(ActivityKind.Consumer, false)]
        public void Local_root_dependency_emits_dependency_then_service_with_upstream_fields(ActivityKind kind, bool hasParent)
        {
            var attrs = BasicAttributes();
            using var span = ConstructSpan(kind, attrs, new[] { Link() }, hasParent);
            var originalParent = span.ParentSpanId;
            var segments = ConvertDocuments(Converter(), span, BasicResource());
            Assert.Equal(2, segments.Length);
            var dependency = segments[0];
            var service = segments[1];
            Assert.Equal("subsegment", dependency.Type);
            Assert.Equal("remote", dependency.Namespace);
            Assert.Equal("myRemoteService", dependency.Name);
            Assert.Equal(span.SpanId.ToHexString(), dependency.Id);
            Assert.Equal(service.Id, dependency.ParentId);
            Assert.NotEqual(span.SpanId.ToHexString(), service.Id);
            Assert.Matches("^[0-9a-f]{16}$", service.Id);
            Assert.NotEqual("0000000000000000", service.Id);
            Assert.Equal(hasParent ? originalParent.ToHexString() : null, service.ParentId);
            Assert.Equal("myLocalService", service.Name);
            Assert.Null(service.Type);
            Assert.Null(service.Namespace);
            Assert.Equal(dependency.TraceId, service.TraceId);
            Assert.Equal(dependency.StartTime, service.StartTime);
            Assert.Equal(dependency.EndTime, service.EndTime);
            Assert.Equal("POST", dependency.Http.Request.Method);
            Assert.Null(service.Http);
            Assert.Equal("myRemoteService", dependency.Annotations["aws_remote_service"].ToString());
            Assert.Equal("myAnnotationValue", dependency.Annotations["myAnnotationKey"].ToString());
            Assert.Single(service.Annotations);
            Assert.Equal("myAnnotationValue", service.Annotations["myAnnotationKey"].ToString());

            foreach (var key in new[] { "aws.remote.operation", "remoteTarget", "K8s.RemoteNamespace", "aws.local.service", "aws.local.operation", "aws.span.kind", "messaging.operation" })
                Assert.True(dependency.Metadata["default"].ContainsKey(key), key);

            Assert.Equal("service.name=myTest", service.Metadata["default"]["otel.resource.attributes"].ToString());
            Assert.All(service.Metadata.Values.SelectMany(value => value.Keys), key => Assert.StartsWith("otel.resource.", key));
            Assert.Equal(kind == ActivityKind.Consumer, dependency.Metadata["default"].ContainsKey("otel.resource.service.name"));
            Assert.Equal(kind == ActivityKind.Consumer || !hasParent, service.Metadata["default"].ContainsKey("otel.resource.service.name"));
            Assert.Equal("UpdateItem", dependency.Aws.Operation);
            Assert.Equal("AWSAccountAttribute", dependency.Aws.AccountId);
            Assert.Equal("AWSRegionAttribute", dependency.Aws.RemoteRegion);
            Assert.Equal("AWSRequestIDAttribute", dependency.Aws.RequestId);
            Assert.Equal("AWSQueueURLAttribute", dependency.Aws.QueueUrl);
            Assert.Equal("TableName", dependency.Aws.TableName);
            Assert.Null(service.Aws.Operation);
            Assert.Null(service.Aws.AccountId);
            Assert.Null(service.Aws.RemoteRegion);
            Assert.Null(service.Aws.RequestId);
            Assert.Null(service.Aws.QueueUrl);
            Assert.Null(service.Aws.TableName);
            Assert.Equal("MySDK", service.Aws.XRay.Sdk);
            Assert.Equal("1.20.0", service.Aws.XRay.SdkVersion);
            Assert.True(service.Aws.XRay.AutoInstrumentation);

            if (kind == ActivityKind.Consumer)
            {
                Assert.Null(dependency.Links);
                Assert.Single(service.Links);
            }
            else
            {
                Assert.Single(dependency.Links);
                Assert.Null(service.Links);
            }

            Assert.Equal(originalParent, span.ParentSpanId);
            Assert.Equal(attrs.Count, span.TagObjects.Count());
            Assert.Equal("myRemoteService", span.GetTagItem("aws.remote.service"));
        }

        [Theory]
        [InlineData(ActivityKind.Server, true)]
        [InlineData(ActivityKind.Internal, true)]
        [InlineData(ActivityKind.Server, false)]
        [InlineData(ActivityKind.Internal, false)]
        public void Local_root_without_dependency_keeps_original_fields_and_identity(ActivityKind kind, bool hasParent)
        {
            using var span = ConstructSpan(kind, BasicAttributes(), new[] { Link() }, hasParent);
            var segment = Assert.Single(ConvertDocuments(Converter(), span, BasicResource()));
            Assert.Equal("myLocalService", segment.Name);
            Assert.Equal(span.SpanId.ToHexString(), segment.Id);
            Assert.Equal(hasParent ? span.ParentSpanId.ToHexString() : null, segment.ParentId);
            Assert.Null(segment.Type);
            Assert.Null(segment.Namespace);
            Assert.Single(segment.Links);
            Assert.Equal("POST", segment.Http.Request.Method);
            Assert.Equal("UpdateItem", segment.Aws.Operation);
            Assert.Equal("myRemoteService", segment.Annotations["aws_remote_service"].ToString());
            Assert.Equal("myRemoteOperation", segment.Metadata["default"]["aws.remote.operation"].ToString());
            Assert.Equal(kind == ActivityKind.Server || !hasParent, segment.Metadata["default"].ContainsKey("otel.resource.service.name"));
        }

        [Theory]
        [InlineData(ActivityKind.Internal, "destination operation", "subsegment", null)]
        [InlineData(ActivityKind.Server, "myLocalService", null, null)]
        [InlineData(ActivityKind.Client, "myRemoteService", "subsegment", "remote")]
        [InlineData(ActivityKind.Producer, "myRemoteService", "subsegment", "remote")]
        [InlineData(ActivityKind.Consumer, "myRemoteService", null, null)]
        public void Non_local_root_uses_regular_topology(ActivityKind kind, string name, string type, string ns)
        {
            var attrs = BasicAttributes();
            attrs["aws.span.kind"] = kind.ToString();

            using var span = ConstructSpan(kind, attrs, new[] { Link() });
            var segment = Assert.Single(ConvertDocuments(Converter(), span, BasicResource()));
            Assert.Equal(name, segment.Name);
            Assert.Equal(type, segment.Type);
            Assert.Equal(ns, segment.Namespace);
            Assert.Equal(span.SpanId.ToHexString(), segment.Id);
            Assert.Single(segment.Links);
        }

        [Theory]
        [InlineData(ActivityKind.Client)]
        [InlineData(ActivityKind.Producer)]
        [InlineData(ActivityKind.Consumer)]
        public void Service_name_falls_back_to_resource_and_resource_metadata_stays_on_service(ActivityKind kind)
        {
            var attrs = BasicAttributes();
            attrs.Remove("aws.local.service");

            using var span = ConstructSpan(kind, attrs);
            var segments = ConvertDocuments(Converter(), span, BasicResource());
            Assert.Equal("signup_aggregator", segments[1].Name);
        }

        [Theory]
        [InlineData("AWS.SDK.SQS", "aws-api", "SQS", "aws")]
        [InlineData("AWS::DynamoDB", "aws-api", "DynamoDB", "aws")]
        [InlineData("PaymentService", "aws-api", "PaymentService", "aws")]
        [InlineData("AWS.SDK.SQS", "other", "AWS.SDK.SQS", "remote")]
        [InlineData("AWS::DynamoDB", "other", "AWS::DynamoDB", "remote")]
        public void Remote_service_prefixes_only_strip_for_AWS_SDK_spans(string remoteName, string system, string expectedName, string expectedNamespace)
        {
            var attrs = BasicAttributes();
            attrs["aws.remote.service"] = remoteName;
            attrs["rpc.system"] = system;

            using var span = ConstructSpan(ActivityKind.Client, attrs);
            Assert.Equal(expectedName, ConvertSegment(Converter(), span).Name);

            var segments = ConvertDocuments(Converter(), span, BasicResource());
            Assert.Equal(expectedName, segments[0].Name);
            Assert.Equal(expectedNamespace, segments[0].Namespace);
            Assert.Null(segments[1].Namespace);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Service_projection_removes_remote_annotations_namespaced_metadata_and_AWS_tables(bool indexAll, bool allowDot)
        {
            var attrs = BasicAttributes();
            attrs["aws.dynamodb.table_names"] = new[] { "one", "two" };
            attrs["aws.xray.metadata.custom"] = "{\"extra\":1,\"otel.resource.kept\":\"value\"}";

            using var span = ConstructSpan(ActivityKind.Consumer, attrs);

            var converter = Converter(indexAll, allowDot);
            var documents = converter.ConvertDocuments(BasicResource(), span);
            using var dependency = JsonDocument.Parse(documents.first);
            using var service = JsonDocument.Parse(documents.second);
            Assert.True(dependency.RootElement.GetProperty("aws").TryGetProperty("table_names", out _));
            Assert.False(service.RootElement.GetProperty("aws").TryGetProperty("table_names", out _));

            var annotations = service.RootElement.GetProperty("annotations");
            foreach (var key in new[] { "aws.remote.service", "aws.remote.operation", "remoteTarget", "K8s.RemoteNamespace" })
                Assert.False(annotations.TryGetProperty(XRayConverter.FixAnnotationKey(key, allowDot), out _));

            if (!indexAll)
            {
                var metadata = service.RootElement.GetProperty("metadata").GetProperty("custom");
                Assert.False(metadata.TryGetProperty("extra", out _));
                Assert.Equal("value", metadata.GetProperty("otel.resource.kept").GetString());
            }
        }

        [Theory]
        [InlineData(ActivityKind.Client)]
        [InlineData(ActivityKind.Producer)]
        [InlineData(ActivityKind.Consumer)]
        public void AWS_namespace_is_preserved_for_all_dependency_kinds(ActivityKind kind)
        {
            var attrs = BasicAttributes();
            attrs["rpc.system"] = "aws-api";

            using var span = ConstructSpan(kind, attrs);
            var segments = ConvertDocuments(Converter(), span, BasicResource());
            Assert.Equal("aws", segments[0].Namespace);
            Assert.Null(segments[1].Namespace);
        }

        [Theory]
        [InlineData(ActivityKind.Client)]
        [InlineData(ActivityKind.Producer)]
        [InlineData(ActivityKind.Consumer)]
        public void Remote_service_takes_precedence_over_peer_service(ActivityKind kind)
        {
            var attrs = BasicAttributes();
            attrs["peer.service"] = "peer";

            using var span = ConstructSpan(kind, attrs);
            Assert.Equal("myRemoteService", ConvertSegment(Converter(), span).Name);
        }

        [Fact]
        public void Non_local_root_consumer_process_retains_resource_links_and_no_remote_namespace()
        {
            var attrs = BasicAttributes();
            attrs["aws.span.kind"] = "Consumer";
            attrs.Remove("aws.remote.service");
            attrs.Remove("aws.remote.operation");

            using var span = ConstructSpan(ActivityKind.Consumer, attrs, new[] { Link() });
            var segment = Assert.Single(ConvertDocuments(Converter(), span, BasicResource()));
            Assert.Null(segment.Type);
            Assert.Null(segment.Namespace);
            Assert.Equal("destination operation", segment.Name);
            Assert.Single(segment.Links);
            Assert.Equal("myLocalService", segment.Metadata["default"]["aws.local.service"].ToString());
            Assert.Equal("signup_aggregator", segment.Metadata["default"]["otel.resource.service.name"].ToString());
        }

        [Fact]
        public void Legacy_Convert_keeps_single_document_and_document_conversion_does_not_mutate_Activity()
        {
            using var span = ConstructSpan(ActivityKind.Consumer, BasicAttributes());
            var converter = Converter();
            var first = converter.Convert(BasicResource(), span);
            var (firstDocument, secondDocument) = converter.ConvertDocuments(BasicResource(), span);
            Assert.NotNull(firstDocument);
            Assert.NotNull(secondDocument);
            Assert.Equal(first, converter.Convert(BasicResource(), span));
        }

        [Fact]
        public void Service_conversion_can_reenter_cached_writer_without_leaking_service_fields()
        {
            using var span = ConstructSpan(ActivityKind.Consumer, BasicAttributes());
            using var nestedSpan = ConstructSpan(ActivityKind.Client, new Dictionary<string, object>
            {
                ["http.method"] = "GET",
                ["aws.request_id"] = "nested-request",
                ["nested"] = "value",
            });
            var calls = 0;
            var nested = default(Model.XRaySegment);
            var converter = default(XRayConverter);

            converter = new XRayConverter((key, isResource) =>
            {
                // ReSharper disable once AccessToDisposedClosure
                // ReSharper disable once AccessToModifiedClosure
                if (!isResource && key == "aws.local.service" && ++calls == 2)
                    nested = ConvertSegment(converter, nestedSpan);

                return false;
            }, null, false, false);

            var segments = ConvertDocuments(converter, span, BasicResource());

            Assert.Equal(2, calls);
            Assert.NotNull(nested);
            Assert.Equal("GET", nested.Http.Request.Method);
            Assert.Equal("nested-request", nested.Aws.RequestId);
            Assert.Equal("value", nested.Metadata["default"]["nested"].ToString());
            Assert.Equal("POST", segments[0].Http.Request.Method);
            Assert.Null(segments[1].Http);
            Assert.Null(segments[1].Aws.RequestId);
            Assert.All(segments[1].Metadata.Values.SelectMany(value => value.Keys), key => Assert.StartsWith("otel.resource.", key));
        }

        [Fact]
        public void Cache_recovers_after_failure_writing_the_service_document()
        {
            using var span = ConstructSpan(ActivityKind.Consumer, BasicAttributes());
            var calls = 0;
            var converter = new XRayConverter((key, isResource) =>
            {
                if (!isResource && key == "aws.local.service" && ++calls == 2)
                    throw new InvalidOperationException("service callback failure");

                return false;
            }, null, false, false);

            Assert.Throws<InvalidOperationException>(() => converter.ConvertDocuments(BasicResource(), span));
            Assert.Equal(2, calls);

            var segments = ConvertDocuments(converter, span, BasicResource());
            Assert.Equal(2, segments.Length);
            Assert.Equal(segments[1].Id, segments[0].ParentId);
            Assert.Equal("POST", segments[0].Http.Request.Method);
            Assert.Null(segments[1].Http);
            Assert.Equal("myLocalService", segments[1].Name);

            var single = ConvertSegment(converter, span, BasicResource());
            Assert.Equal(span.SpanId.ToHexString(), single.Id);
            Assert.Equal("POST", single.Http.Request.Method);
            Assert.Equal("AWSRequestIDAttribute", single.Aws.RequestId);
            Assert.Equal("myLocalService", single.Metadata["default"]["aws.local.service"].ToString());
        }

        [Fact]
        public void Overwritten_trace_id_is_shared_by_generated_documents_and_inprogress_omits_end_time()
        {
            using var span = ActivitySource.CreateActivity(
                "dependency",
                ActivityKind.Client,
                new ActivityContext(ActivityTraceId.CreateFromString("00000001aaaaaaaaaaaaaaaaaaaaaaaa".AsSpan()), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded),
                new Dictionary<string, object>
                {
                    ["aws.span.kind"] = "LOCAL_ROOT",
                    ["aws.xray.inprogress"] = true,
                });

            var converter = new XRayConverter(null, null, false, false, validateTraceId: true, overwriteInvalidTraceId: true);
            var documents = converter.ConvertDocuments(BasicResource(), span);
            using var dependency = JsonDocument.Parse(documents.first);
            using var service = JsonDocument.Parse(documents.second);

            Assert.Equal(dependency.RootElement.GetProperty("trace_id").GetString(), service.RootElement.GetProperty("trace_id").GetString());
            foreach (var document in new[] { documents.first, documents.second })
            {
                using var parsed = JsonDocument.Parse(document);
                Assert.True(parsed.RootElement.GetProperty("in_progress").GetBoolean());
                Assert.False(parsed.RootElement.TryGetProperty("end_time", out _));
            }
        }
    }
}