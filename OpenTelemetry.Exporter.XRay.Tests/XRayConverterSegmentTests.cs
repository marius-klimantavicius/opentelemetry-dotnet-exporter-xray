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
    public class XRayConverterSegmentTests : XRayTest
    {
        [Fact]
        public void Should_map_client_span_with_rpc_aws_sdk_client_attributes()
        {
            var spanName = "AmazonDynamoDB.getItem";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var user = "testingT";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeHttpHost] = "dynamodb.us-east-1.amazonaws.com",
                [XRayConventions.AttributeHttpTarget] = "/",
                [XRayConventions.AttributeRpcService] = "DynamoDB",
                [XRayConventions.AttributeRpcMethod] = "GetItem",
                [XRayConventions.AttributeRpcSystem] = "aws-api",
                [XRayConventions.AttributeAwsRequestId] = "18BO1FEPJSSAOGNJEDPTPCMIU7VV4KQNSO5AEMVJF66Q9ASUAAJG",
                [XRayConventions.AttributeAwsTableName] = "otel-dev-Testing",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Unset, "OK", attributes);

            var segment = ConvertDefault(span, resource, out var segmentDocument);
            Assert.Equal("DynamoDB", segment.Name);
            Assert.Equal(XRayAwsConventions.AttributeCloudProviderAws, segment.Namespace);
            Assert.Equal("GetItem", segment.Aws.Operation);
            Assert.Equal("subsegment", segment.Type);

            Assert.DoesNotContain(user, segmentDocument);
            Assert.DoesNotContain("user", segmentDocument);
        }

        [Fact]
        public void Should_map_client_span_with_legacy_aws_sdk_client_attributes()
        {
            var spanName = "AmazonDynamoDB.getItem";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var user = "testingT";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeHttpHost] = "dynamodb.us-east-1.amazonaws.com",
                [XRayConventions.AttributeHttpTarget] = "/",
                [XRayConventions.AttributeAwsService] = "DynamoDB",
                [XRayConventions.AttributeRpcMethod] = "IncorrectAWSSDKOperation",
                [XRayConventions.AttributeAwsOperation] = "GetItem",
                [XRayConventions.AttributeAwsRequestId] = "18BO1FEPJSSAOGNJEDPTPCMIU7VV4KQNSO5AEMVJF66Q9ASUAAJG",
                [XRayConventions.AttributeAwsTableName] = "otel-dev-Testing",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Unset, "OK", attributes);

            var segment = ConvertDefault(span, resource, out var segmentDocument);
            Assert.Equal("DynamoDB", segment.Name);
            Assert.Equal(XRayAwsConventions.AttributeCloudProviderAws, segment.Namespace);
            Assert.Equal("GetItem", segment.Aws.Operation);
            Assert.Equal("subsegment", segment.Type);

            Assert.DoesNotContain(user, segmentDocument);
            Assert.DoesNotContain("user", segmentDocument);
        }

        [Fact]
        public void Should_map_client_span_with_peer_service()
        {
            var spanName = "AmazonDynamoDB.getItem";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeHttpHost] = "dynamodb.us-east-1.amazonaws.com",
                [XRayConventions.AttributeHttpTarget] = "/",
                [XRayConventions.AttributePeerService] = "cats-table",
                [XRayConventions.AttributeAwsService] = "DynamoDB",
                [XRayConventions.AttributeAwsOperation] = "GetItem",
                [XRayConventions.AttributeAwsRequestId] = "18BO1FEPJSSAOGNJEDPTPCMIU7VV4KQNSO5AEMVJF66Q9ASUAAJG",
                [XRayConventions.AttributeAwsTableName] = "otel-dev-Testing",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Unset, "OK", attributes);

            var segment = ConvertDefault(span, resource);
            Assert.Equal("cats-table", segment.Name);
        }

        [Fact]
        public void Should_map_server_span_with_internal_server_error()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var errorMessage = "java.lang.NullPointerException";
            var userAgent = "PostmanRuntime/7.21.0";
            var enduser = "go.tester@example.com";
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.org/api/locations",
                [XRayConventions.AttributeHttpTarget] = "/api/locations",
                [XRayConventions.AttributeHttpStatusCode] = 500,
                [XRayConventions.AttributeHttpStatusText] = "java.lang.NullPointerException",
                [XRayConventions.AttributeHttpUserAgent] = userAgent,
                [XRayConventions.AttributeEndUserId] = enduser,
            };
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, errorMessage, attributes);
            ConstructTimedEventsWithReceivedMessageEvent(span);

            var segment = ConvertDefault(span, resource);
            Assert.NotNull(segment.Cause);
            Assert.Equal("signup_aggregator", segment.Name);
            Assert.True(segment.IsFault);
        }

        [Fact]
        public void Should_map_server_span_with_throttle()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var errorMessage = "java.lang.NullPointerException";
            var userAgent = "PostmanRuntime/7.21.0";
            var enduser = "go.tester@example.com";
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.org/api/locations",
                [XRayConventions.AttributeHttpTarget] = "/api/locations",
                [XRayConventions.AttributeHttpStatusCode] = 429,
                [XRayConventions.AttributeHttpStatusText] = "java.lang.NullPointerException",
                [XRayConventions.AttributeHttpUserAgent] = userAgent,
                [XRayConventions.AttributeEndUserId] = enduser,
            };
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, errorMessage, attributes);
            ConstructTimedEventsWithReceivedMessageEvent(span);

            var segment = ConvertDefault(span, resource);
            Assert.NotNull(segment.Cause);
            Assert.Equal("signup_aggregator", segment.Name);
            Assert.False(segment.IsFault);
            Assert.True(segment.IsError);
            Assert.True(segment.IsThrottle);
        }

        [Fact]
        public void Should_map_server_span_with_no_parent()
        {
            var spanName = "/api/locations";
            var parentSpanID = default(ActivitySpanId);
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Ok, "OK", new List<KeyValuePair<string, object>>());

            var segment = ConvertDefault(span, resource);
            Assert.Null(segment.ParentId);
        }

        [Fact]
        public void Should_map_span_with_no_parent()
        {
            var activityContext = new ActivityContext(
                XRayTraceId.Generate(),
                default(ActivitySpanId),
                ActivityTraceFlags.Recorded);
            var span = ActivitySource.CreateActivity(
                "my-topic send",
                ActivityKind.Producer,
                activityContext);

            var segment = ConvertDefault(span);
            Assert.Null(segment.ParentId);
            Assert.Null(segment.Type);
        }

        [Fact]
        public void Should_map_span_with_no_status()
        {
            var activityContext = new ActivityContext(
                XRayTraceId.Generate(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded);
            var span = ActivitySource.CreateActivity(
                "",
                ActivityKind.Server,
                activityContext);

            Assert.NotNull(span);

            span.SetStartTime(DateTime.UtcNow);
            span.SetEndTime(DateTime.UtcNow.AddSeconds(10));

            ConvertDefault(span);
        }

        [Fact]
        public void Should_map_client_span_with_db_component()
        {
            var spanName = "call update_user_preference( ?, ?, ? )";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var enterpriseAppID = "25F2E73B-4769-4C79-9DF3-7EBE85D571EA";
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeDbSystem] = "mysql",
                [XRayConventions.AttributeDbName] = "customers",
                [XRayConventions.AttributeDbStatement] = spanName,
                [XRayConventions.AttributeDbUser] = "userprefsvc",
                [XRayConventions.AttributeDbConnectionString] = "mysql://db.dev.example.com:3306",
                [XRayConventions.AttributeNetPeerName] = "db.dev.example.com",
                [XRayConventions.AttributeNetPeerPort] = "3306",
                ["enterprise.app.id"] = enterpriseAppID,
            };
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Unset, "OK", attributes);

            var converter = new XRayConverter(null, null, false, false);
            var segment = ConvertSegment(converter, span, resource);
            Assert.NotNull(segment.Sql);
            Assert.NotNull(segment.Service);
            Assert.NotNull(segment.Aws);
            Assert.NotNull(segment.Metadata);
            Assert.Null(segment.Annotations);
            Assert.Equal(enterpriseAppID, segment.Metadata["default"]["enterprise.app.id"].ToString());
            Assert.Null(segment.Cause);
            Assert.Null(segment.Http);
            Assert.Equal("customers@db.dev.example.com", segment.Name);
            Assert.False(segment.IsFault);
            Assert.False(segment.IsError);
            Assert.Equal("remote", segment.Namespace);
        }

        [Fact]
        public void Should_map_client_span_with_http_host()
        {
            var spanName = "GET /";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeNetPeerIp] = "2607:f8b0:4000:80c::2004",
                [XRayConventions.AttributeNetPeerPort] = "9443",
                [XRayConventions.AttributeHttpTarget] = "/",
                [XRayConventions.AttributeHttpHost] = "foo.com",
                [XRayConventions.AttributeNetPeerName] = "bar.com",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Unset, "OK", attributes);

            var segment = ConvertDefault(span, resource);
            Assert.Equal("foo.com", segment.Name);
        }

        [Fact]
        public void Should_map_client_span_without_http_host()
        {
            var spanName = "GET /";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeNetPeerIp] = "2607:f8b0:4000:80c::2004",
                [XRayConventions.AttributeNetPeerPort] = "9443",
                [XRayConventions.AttributeHttpTarget] = "/",
                [XRayConventions.AttributeNetPeerName] = "bar.com",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Unset, "OK", attributes);

            var segment = ConvertDefault(span, resource);
            Assert.Equal("bar.com", segment.Name);
        }

        [Fact]
        public void Should_map_client_span_with_rpc_host()
        {
            var spanName = "GET /com.foo.AnimalService/GetCats";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "https",
                [XRayConventions.AttributeNetPeerIp] = "2607:f8b0:4000:80c::2004",
                [XRayConventions.AttributeNetPeerPort] = "9443",
                [XRayConventions.AttributeHttpTarget] = "/com.foo.AnimalService/GetCats",
                [XRayConventions.AttributeRpcService] = "com.foo.AnimalService",
                [XRayConventions.AttributeNetPeerName] = "bar.com",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Unset, "OK", attributes);

            var segment = ConvertDefault(span, resource);
            Assert.Equal("com.foo.AnimalService", segment.Name);
        }

        [Fact]
        public void Should_fail_with_invalid_trace_id()
        {
            var spanName = "platformapi.widgets.searchWidgets";
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "GET",
                [XRayConventions.AttributeHttpScheme] = "ipv6",
                [XRayConventions.AttributeNetPeerIp] = "2607:f8b0:4000:80c::2004",
                [XRayConventions.AttributeNetPeerPort] = "9443",
                [XRayConventions.AttributeHttpTarget] = spanName,
            };
            var resource = ConstructDefaultResource();
            var traceId = XRayTraceId.Generate();
            traceId = ActivityTraceId.CreateFromString("11" + traceId.ToHexString().Substring(2));

            var activityContext = new ActivityContext(
                traceId,
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded);
            var span = ActivitySource.CreateActivity(
                spanName,
                ActivityKind.Client,
                activityContext,
                attributes);
            Assert.NotNull(span);

            span.SetStartTime(DateTime.UtcNow);
            span.SetEndTime(DateTime.UtcNow.AddSeconds(2));
            ConstructTimedEventsWithReceivedMessageEvent(span);

            var converter = new XRayConverter(null, null, false, false, true);
            var segmentDocument = converter.Convert(resource, span);
            Assert.Null(segmentDocument);
        }

        [Fact]
        public void Should_fail_with_expired_trace_id()
        {
            var traceId = XRayTraceId.Generate();
            var maxAge = 60 * 60 * 24 * 30;
            var expiredEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - maxAge - 1;
            traceId = ActivityTraceId.CreateFromString($"{expiredEpoch:x8}{traceId.ToHexString().Substring(8)}");

            var activityContext = new ActivityContext(
                traceId,
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded);
            var span = ActivitySource.CreateActivity(
                "",
                ActivityKind.Client,
                activityContext);
            Assert.NotNull(span);

            span.SetStartTime(DateTime.UtcNow);
            span.SetEndTime(DateTime.UtcNow.AddSeconds(2));

            var converter = new XRayConverter(null, null, false, false, true);
            var segmentDocument = converter.Convert(Resource.Empty, span);
            Assert.Null(segmentDocument);
        }

        [Fact]
        public void Should_fix_segment_name()
        {
            var validName = "EP @ test_15.testing-d\u00F6main.org#GO";
            var fixedName = XRayConverter.FixSegmentName(validName);
            Assert.Equal(validName, fixedName);

            var invalidName = "<subDomain>.example.com";
            fixedName = XRayConverter.FixSegmentName(invalidName);
            Assert.Equal("subDomain.example.com", fixedName);

            var fullyInvalidName = "<>";
            fixedName = XRayConverter.FixSegmentName(fullyInvalidName);
            Assert.Equal("span", fixedName);
        }

        [Fact]
        public void Should_fix_annotation_key()
        {
            var validKey = "Key_1";
            var fixedKey = XRayConverter.FixAnnotationKey(validKey);
            Assert.Equal(validKey, fixedKey);

            var invalidKey = "Key@1";
            fixedKey = XRayConverter.FixAnnotationKey(invalidKey);
            Assert.Equal("Key_1", fixedKey);
        }

        [Fact]
        public void Should_map_server_span_with_null_attributes()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", Array.Empty<KeyValuePair<string, object>>());
            ConstructTimedEventsWithSentMessageEvent(span);

            var segment = ConvertDefault(span, resource);
            Assert.NotNull(segment.Cause);
            Assert.Equal("signup_aggregator", segment.Name);
            Assert.True(segment.IsFault);
        }

        [Fact]
        public void Should_map_span_with_attributes_default_not_indexed()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                ["attr1@1"] = "val1",
                ["attr2@2"] = "val2",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", attributes);

            var converter = new XRayConverter(null, null, false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Null(segment.Annotations);
            Assert.Equal("val1", segment.Metadata["default"]["attr1@1"].ToString());
            Assert.Equal("val2", segment.Metadata["default"]["attr2@2"].ToString());
            Assert.Equal("string", segment.Metadata["default"]["otel.resource.string.key"].ToString());
            Assert.Equal(10, ((JsonElement)segment.Metadata["default"]["otel.resource.int.key"]).GetInt32());
            Assert.Equal(5.0, ((JsonElement)segment.Metadata["default"]["otel.resource.double.key"]).GetDouble());
            Assert.True(((JsonElement)segment.Metadata["default"]["otel.resource.bool.key"]).GetBoolean());

            var metadataArray = ((JsonElement)segment.Metadata["default"]["otel.resource.array.key"]).EnumerateArray().Select(s => s.ToString()).ToList();
            Assert.Equal(new[] { "foo", "bar" }, metadataArray);
        }

        [Fact]
        public void Should_map_span_with_resource_not_stored_if_subsegment()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                ["attr1@1"] = "val1",
                ["attr2@2"] = "val2",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", attributes);

            var converter = new XRayConverter(null, null, false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Null(segment.Annotations);
            Assert.Equal("val1", segment.Metadata["default"]["attr1@1"].ToString());
            Assert.Equal("val2", segment.Metadata["default"]["attr2@2"].ToString());

            var def = segment.Metadata["default"];
            Assert.DoesNotContain("otel.resource.string.key", def);
            Assert.DoesNotContain("otel.resource.int.key", def);
            Assert.DoesNotContain("otel.resource.double.key", def);
            Assert.DoesNotContain("otel.resource.bool.key", def);
            Assert.DoesNotContain("otel.resource.map.key", def);
            Assert.DoesNotContain("otel.resource.array.key", def);
        }

        [Fact]
        public void Should_map_span_with_attributes_partially_indexed()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                ["attr1@1"] = "val1",
                ["attr2@2"] = "val2",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", attributes);

            var converter = new XRayConverter(null, new[] { "attr1@1", "not_exist" }, false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal("val1", segment.Annotations["attr1_1"].ToString());
            Assert.Equal("val2", segment.Metadata["default"]["attr2@2"].ToString());
        }

        [Fact]
        public void Should_map_span_with_attributes_partially_indexed_function()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                ["attr1@1"] = "val1",
                ["attr2@2"] = "val2",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", attributes);

            var converter = new XRayConverter((name, _) => name == "attr1@1" || name == "not_exist", false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal("val1", segment.Annotations["attr1_1"].ToString());
            Assert.Equal("val2", segment.Metadata["default"]["attr2@2"].ToString());
        }

        [Fact]
        public void Should_map_span_with_attributes_partially_indexed_function_and_list()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                ["attr1@1"] = "val1",
                ["attr2@2"] = "val2",
                ["attr3@3"] = "val3",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", attributes);

            var converter = new XRayConverter((name, _) => name == "attr1@1" || name == "not_exist", new[] { "attr3@3" }, false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal("val1", segment.Annotations["attr1_1"].ToString());
            Assert.Equal("val3", segment.Annotations["attr3_3"].ToString());
            Assert.Equal("val2", segment.Metadata["default"]["attr2@2"].ToString());
        }

        [Fact]
        public void Should_map_span_with_attributes_all_indexed()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                ["attr1@1"] = "val1",
                ["attr2@2"] = "val2",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", attributes);

            var converter = new XRayConverter(null, new[] { "attr1@1", "not_exist" }, true, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal("val1", segment.Annotations["attr1_1"].ToString());
            Assert.Equal("val2", segment.Annotations["attr2_2"].ToString());
        }

        [Fact]
        public void Should_map_span_with_attributes_all_indexed_function()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var attributes = new Dictionary<string, object>
            {
                ["attr1@1"] = "val1",
                ["attr2@2"] = "val2",
            };
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", attributes);

            var converter = new XRayConverter((name, _) => name == "attr1@1" || name == "not_exist", true, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal("val1", segment.Annotations["attr1_1"].ToString());
            Assert.Equal("val2", segment.Annotations["attr2_2"].ToString());
        }

        [Fact]
        public void Should_index_resource_attributes()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var converter = new XRayConverter(null, new[]
            {
                "otel.resource.string.key",
                "otel.resource.int.key",
                "otel.resource.double.key",
                "otel.resource.bool.key",
                "otel.resource.map.key",
                "otel.resource.array.key",
            }, false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal("string", segment.Annotations["otel_resource_string_key"].ToString());
            Assert.Equal(10, ((JsonElement)segment.Annotations["otel_resource_int_key"]).GetInt32());
            Assert.Equal(5.0, ((JsonElement)segment.Annotations["otel_resource_double_key"]).GetDouble());
            Assert.True(((JsonElement)segment.Annotations["otel_resource_bool_key"]).GetBoolean());

            var metadataArray = ((JsonElement)segment.Metadata["default"]["otel.resource.array.key"]).EnumerateArray().Select(s => s.ToString()).ToList();
            Assert.Collection(metadataArray,
                s => Assert.Equal("foo", s),
                s => Assert.Equal("bar", s));
        }

        [Fact]
        public void Should_index_resource_attributes_function()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var set = new HashSet<string>(new[]
            {
                "string.key",
                "int.key",
                "double.key",
                "bool.key",
                "map.key",
                "array.key",
            }, StringComparer.Ordinal);
            var converter = new XRayConverter((name, isResource) => isResource && set.Contains(name), false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal("string", segment.Annotations["otel_resource_string_key"].ToString());
            Assert.Equal(10, ((JsonElement)segment.Annotations["otel_resource_int_key"]).GetInt32());
            Assert.Equal(5.0, ((JsonElement)segment.Annotations["otel_resource_double_key"]).GetDouble());
            Assert.True(((JsonElement)segment.Annotations["otel_resource_bool_key"]).GetBoolean());

            var metadataArray = ((JsonElement)segment.Metadata["default"]["otel.resource.array.key"]).EnumerateArray().Select(s => s.ToString()).ToList();
            Assert.Collection(metadataArray,
                s => Assert.Equal("foo", s),
                s => Assert.Equal("bar", s));
        }

        [Fact]
        public void Should_index_resource_attributes_function_and_list()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resource = ConstructDefaultResource();
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var converter = new XRayConverter((name, isResource) =>
                isResource && (
                    name == "bool.key" ||
                    name == "map.key" ||
                    name == "array.key"
                ), new[]
            {
                "otel.resource.string.key",
                "otel.resource.int.key",
                "otel.resource.double.key",
            }, false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.Equal("string", segment.Annotations["otel_resource_string_key"].ToString());
            Assert.Equal(10, ((JsonElement)segment.Annotations["otel_resource_int_key"]).GetInt32());
            Assert.Equal(5.0, ((JsonElement)segment.Annotations["otel_resource_double_key"]).GetDouble());
            Assert.True(((JsonElement)segment.Annotations["otel_resource_bool_key"]).GetBoolean());

            var metadataArray = ((JsonElement)segment.Metadata["default"]["otel.resource.array.key"]).EnumerateArray().Select(s => s.ToString()).ToList();
            Assert.Collection(metadataArray,
                s => Assert.Equal("foo", s),
                s => Assert.Equal("bar", s));
        }

        [Fact]
        public void Should_not_index_resource_attributes_if_subsegment()
        {
            var spanName = "/api/locations";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resource = ConstructDefaultResource();
            var span = ConstructClientSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var converter = new XRayConverter(null, new[]
            {
                "otel.resource.string.key",
                "otel.resource.int.key",
                "otel.resource.double.key",
                "otel.resource.bool.key",
                "otel.resource.map.key",
                "otel.resource.array.key",
            }, false, false);

            var segment = ConvertSegment(converter, span, resource);
            Assert.Null(segment.Annotations);
            Assert.Null(segment.Metadata);
        }

        [Fact]
        public void Should_map_origin_not_aws()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = "gcp",
                [XRayConventions.AttributeHostId] = "instance-123",
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Null(segment.Origin);
        }

        [Fact]
        public void Should_map_origin_ec2()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEc2,
                [XRayConventions.AttributeHostId] = "instance-123",
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Equal(XRayAwsConventions.OriginEc2, segment.Origin);
        }

        [Fact]
        public void Should_map_origin_ecs()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEcs,
                [XRayConventions.AttributeHostId] = "instance-123",
                [XRayConventions.AttributeContainerName] = "container-123",
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Equal(XRayAwsConventions.OriginEcs, segment.Origin);
        }

        [Fact]
        public void Should_map_origin_ecs_ec2()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEcs,
                [XRayConventions.AttributeAwsEcsLaunchType] = XRayAwsConventions.AttributeAwsEcsLaunchTypeEc2,
                [XRayConventions.AttributeHostId] = "instance-123",
                [XRayConventions.AttributeContainerName] = "container-123",
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Equal(XRayAwsConventions.OriginEcsEc2, segment.Origin);
        }

        [Fact]
        public void Should_map_origin_ecs_fargate()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEcs,
                [XRayConventions.AttributeAwsEcsLaunchType] = XRayAwsConventions.AttributeAwsEcsLaunchTypeFargate,
                [XRayConventions.AttributeHostId] = "instance-123",
                [XRayConventions.AttributeContainerName] = "container-123",
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Equal(XRayAwsConventions.OriginEcsFargate, segment.Origin);
        }

        [Fact]
        public void Should_map_origin_eb()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsElasticBeanstalk,
                [XRayConventions.AttributeHostId] = "instance-123",
                [XRayConventions.AttributeContainerName] = "container-123",
                [XRayConventions.AttributeServiceInstanceId] = "service-123",
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Equal(XRayAwsConventions.OriginElasticBeanstalk, segment.Origin);
        }

        [Fact]
        public void Should_map_origin_eks()
        {
            var instanceID = "i-00f7c0bcb26da2a99";
            var containerName = "signup_aggregator-x82ufje83";
            var containerID = "0123456789A";
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEks,
                [XRayConventions.AttributeCloudAccountId] = "123456789",
                [XRayConventions.AttributeCloudAvailabilityZone] = "us-east-1c",
                [AttributeContainerImageName] = "otel/signupaggregator",
                [XRayConventions.AttributeContainerImageTag] = "v1",
                [XRayConventions.AttributeK8SClusterName] = "production",
                [AttributeK8SNamespaceName] = "default",
                [AttributeK8SDeploymentName] = "signup_aggregator",
                [XRayConventions.AttributeK8SPodName] = "my-deployment-65dcf7d447-ddjnl",
                [XRayConventions.AttributeContainerName] = containerName,
                [XRayConventions.AttributeContainerId] = containerID,
                [XRayConventions.AttributeHostId] = instanceID,
                [XRayConventions.AttributeHostType] = "m5.xlarge",
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Equal(XRayAwsConventions.OriginEks, segment.Origin);
        }

        [Fact]
        public void Should_map_origin_app_runner()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsAppRunner,
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Equal(XRayAwsConventions.OriginAppRunner, segment.Origin);
        }

        [Fact]
        public void Should_map_origin_blank()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Null(segment.Origin);
        }

        [Fact]
        public void Should_prefer_infra_service_origin()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resourceAttributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEc2,
                [XRayConventions.AttributeK8SClusterName] = "cluster-123",
                [XRayConventions.AttributeHostId] = "instance-123",
                [XRayConventions.AttributeContainerName] = "container-123",
                [XRayConventions.AttributeServiceInstanceId] = "service-123",
            };
            var resource = new Resource(resourceAttributes);
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            var segment = ConvertDefault(span, resource);

            Assert.Equal(XRayAwsConventions.OriginEc2, segment.Origin);
        }

        [Fact]
        public void Should_map_filtered_attributes_metadata()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resource = Resource.Empty;
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            span.SetTag("string_value", "value");
            span.SetTag("int_value", 123);
            span.SetTag("float_value", 456.78);
            span.SetTag("bool_value", false);
            span.SetTag("null_value", null);
            span.SetTag("array_value", new[] { 12, 34, 56 });
            span.SetTag("map_value", new Dictionary<string, object>
            {
                ["value1"] = -987.65,
                ["value2"] = true,
            });

            var converter = new XRayConverter(null, null, false, false);
            var segment = ConvertSegment(converter, span, resource);

            Assert.NotNull(segment.Metadata);
            Assert.Contains("default", segment.Metadata);
            var def = segment.Metadata["default"];
            Assert.DoesNotContain("null_value", def);
            Assert.Equal("value", def["string_value"].ToString());
            Assert.Equal(123, ((JsonElement)def["int_value"]).GetInt32());
            Assert.Equal(456.78, ((JsonElement)def["float_value"]).GetDouble());
            Assert.False(((JsonElement)def["bool_value"]).GetBoolean());

            var array = ((JsonElement)def["array_value"]).EnumerateArray().Select(s => s.GetInt32()).ToList();
            Assert.Equal(new[] { 12, 34, 56 }, array);

            var map = ((JsonElement)def["map_value"]).EnumerateObject().ToDictionary(s => s.Name, s =>
            {
                switch (s.Value.ValueKind)
                {
                    case JsonValueKind.Number:
                        return s.Value.GetDouble();
                    case JsonValueKind.False:
                        return false;
                    case JsonValueKind.True:
                        return true;
                }

                return default(object);
            });

            Assert.Equal(-987.65, map["value1"]);
            Assert.Equal(true, map["value2"]);
        }

        [Fact]
        public void Should_map_supported_primitive_types_for_annotations()
        {
            var spanName = "/test";
            var parentSpanID = ActivitySpanId.CreateRandom();
            var resource = Resource.Empty;
            var span = ConstructServerSpan(parentSpanID, spanName, ActivityStatusCode.Error, "OK", null);

            span.SetTag("byte_value", (byte)15);
            span.SetTag("decimal_value", 24M);
            span.SetTag("short_value", (short)21);
            span.SetTag("sbyte_value", (sbyte)96);
            span.SetTag("float_value", 3.14F);
            span.SetTag("ushort_value", (ushort)633);
            span.SetTag("uint_value", (uint)74556);
            span.SetTag("ulong_value", 5222658414UL);

            span.SetTag("char_value", 'a');
            var dateTime = DateTime.UtcNow.AddMinutes(-3);
            span.SetTag("DateTime_value", dateTime);

            var timeSpan = TimeSpan.FromSeconds(117);
            span.SetTag("TimeSpan_value", timeSpan);

            var dateTimeOffset = DateTimeOffset.UtcNow.AddDays(-2);
            span.SetTag("DateTimeOffset_value", dateTimeOffset);

            var guid = Guid.NewGuid();
            span.SetTag("Guid_value", guid);

            var uri = new Uri("https://learn.example.com/path?hello=world");
            span.SetTag("Uri_value", uri);

            var version = new Version(23, 34);
            span.SetTag("Version_value", version);

            var converter = new XRayConverter(null, null, true, false);
            var segment = ConvertSegment(converter, span, resource);
            var annotations = segment.Annotations;

            Assert.Equal((byte)15, ((JsonElement)annotations["byte_value"]).GetByte());
            Assert.Equal(24M, ((JsonElement)annotations["decimal_value"]).GetDecimal());
            Assert.Equal((short)21, ((JsonElement)annotations["short_value"]).GetInt16());
            Assert.Equal((sbyte)96, ((JsonElement)annotations["sbyte_value"]).GetSByte());
            Assert.Equal(3.14F, ((JsonElement)annotations["float_value"]).GetSingle());
            Assert.Equal((ushort)633, ((JsonElement)annotations["ushort_value"]).GetUInt16());
            Assert.Equal((uint)74556, ((JsonElement)annotations["uint_value"]).GetUInt32());
            Assert.Equal(5222658414UL, ((JsonElement)annotations["ulong_value"]).GetUInt64());
            Assert.Equal("a", ((JsonElement)annotations["char_value"]).GetString());
            Assert.Equal(dateTime, ((JsonElement)annotations["DateTime_value"]).GetDateTime());
            Assert.Equal(dateTimeOffset, ((JsonElement)annotations["DateTimeOffset_value"]).GetDateTimeOffset());
            Assert.Equal(guid, ((JsonElement)annotations["Guid_value"]).GetGuid());
            Assert.Equal(uri.ToString(), ((JsonElement)annotations["Uri_value"]).GetString());

#if NET6_0_OR_GREATER
            Assert.Equal(timeSpan.ToString(), ((JsonElement)annotations["TimeSpan_value"]).GetString());
            Assert.Equal(version.ToString(), ((JsonElement)annotations["Version_value"]).GetString());
#endif
        }

        private Activity ConstructClientSpan(ActivitySpanId parentSpanId, string name, ActivityStatusCode code, string message, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            var traceId = XRayTraceId.Generate();
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddMilliseconds(-215);

            var parentContext = new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded);
            var span = ActivitySource.CreateActivity(name, ActivityKind.Client, parentContext, attributes);
            Assert.NotNull(span);

            span.SetStartTime(startTime);
            span.SetEndTime(endTime);
            span.SetStatus(code, message);

            return span;
        }

        private Activity ConstructServerSpan(ActivitySpanId parentSpanId, string name, ActivityStatusCode code, string message, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            var traceId = XRayTraceId.Generate();
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddMilliseconds(-215);

            var parentContext = new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded);
            var span = ActivitySource.CreateActivity(name, ActivityKind.Server, parentContext, attributes);
            Assert.NotNull(span);

            span.SetStartTime(startTime);
            span.SetEndTime(endTime);
            span.SetStatus(code, message);

            return span;
        }
    }
}