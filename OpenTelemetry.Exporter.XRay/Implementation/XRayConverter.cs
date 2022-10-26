using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTelemetry.Resources;

#if NET6_0_OR_GREATER
using System.Text.Json.Nodes;
#endif

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private const double TicksPerMillisecond = 10000;
        private const double TicksPerSecond = TicksPerMillisecond * 1000;

        private const string DefaultSegmentName = "span";
        private const string ResourceKeyPrefix = "otel.resource.";
        private const string AnnotationResourceKeyPrefix = "otel_resource_";
        private const int MaxSegmentNameLength = 200;

        private static readonly Regex _invalidSpanCharacters = new Regex(@"[^ 0-9\p{L}_.:/%&#=+,\\\-@]", RegexOptions.Compiled);
        private static readonly Regex _invalidAnnotationCharacters = new Regex(@"[^0-9a-zA-Z]", RegexOptions.Compiled);

        private readonly HashSet<string> _indexedAttributes;
        private readonly HashSet<string> _indexedResourceAttributes;
        private readonly bool _indexAllAttributes;
        private readonly bool _indexActivityNames;
        private readonly bool _validateTraceId;

        [ThreadStatic]
        private static XRayConverterCache _cache;

        public XRayConverter(IEnumerable<string> indexedAttributes, bool indexAllAttributes, bool indexActivityNames, bool validateTraceId = false)
        {
            _indexedAttributes = new HashSet<string>(indexedAttributes ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            _indexedResourceAttributes = new HashSet<string>(
                (indexedAttributes ?? Enumerable.Empty<string>())
                    .Where(s => s.StartsWith(ResourceKeyPrefix, StringComparison.Ordinal))
                    .Select(s => s.Substring(ResourceKeyPrefix.Length))
                , StringComparer.Ordinal);
            _indexAllAttributes = indexAllAttributes;
            _indexActivityNames = indexActivityNames;
            _validateTraceId = validateTraceId;
        }

        public string Convert(Resource resource, Activity span)
        {
            var segmentType = default(string);
            var storeResource = true;

            var traceId = span.TraceId.ToHexString();
            if (!IsValidXRayTraceId(traceId))
                return null;
            
            if (span.Kind != ActivityKind.Server
                && span.ParentSpanId.ToHexString() != "0000000000000000")
            {
                segmentType = "subsegment";
                storeResource = false;
            }

            var cache = _cache ?? new XRayConverterCache();
            try
            {
                var resourceAttributes = cache.ResourceAttributes;
                var attributes = cache.SpanTags;
                var writer = cache.Writer;
                var buffer = cache.Buffer;

                resourceAttributes.Initialize(resource.Attributes);
                attributes.Initialize(span.TagObjects);
                buffer.InitializeEmptyInstance(1024);

                var context = new XRayConverterContext(resource, resourceAttributes, span, attributes, writer);

                var (name, @namespace) = ResolveName(context);
                attributes.ResetConsume();
                
                var startTime = (span.StartTimeUtc - DateTime.UnixEpoch).TotalSeconds;
                var endTime = startTime + span.Duration.TotalSeconds;

                writer.WriteStartObject();
                writer.WriteString(XRayField.Name, name);
                writer.WriteString(XRayField.Id, span.SpanId.ToHexString());
                WriteXRayTraceId(writer, traceId);
                writer.WriteNumber(XRayField.StartTime, startTime);
                writer.WriteNumber(XRayField.EndTime, endTime);
                if (span.ParentSpanId.ToHexString() != "0000000000000000")
                    writer.WriteString(XRayField.ParentId, span.ParentSpanId.ToHexString());
                if (@namespace != null)
                    writer.WriteString(XRayField.Namespace, @namespace);
                if (segmentType != null)
                    writer.WriteString(XRayField.Type, segmentType);

                WriteHttp(context);
                WriteCause(context);

                var origin = DetermineAwsOrigin(context);
                if (origin != null)
                    writer.WriteString(XRayField.Origin, origin);

                WriteAws(context);
                WriteService(context);
                WriteSql(context);
                WriteXRayAttributes(context, storeResource);

                writer.WriteEndObject();
                writer.Flush();

                return Encoding.UTF8.GetString(buffer.WrittenMemory.Span);
            }
            finally
            {
                cache.Return();
                _cache = cache;
            }
        }

        private (string name, string @namespace) ResolveName(in XRayConverterContext context)
        {
            var name = default(string);
            var @namespace = default(string);

            var attributes = context.SpanTags;
            if (attributes.TryGetAttributePeerService(out var peerService))
                name = peerService.AsString();

            if (attributes.TryGetAttributeRpcSystem(out var rpcSystem))
            {
                if (rpcSystem.AsString() == "aws-api")
                    @namespace = XRayAwsConventions.AttributeCloudProviderAws;
            }

            if (string.IsNullOrEmpty(name))
            {
                if (attributes.TryGetAttributeAwsService(out var awsService))
                {
                    name = awsService.AsString();
                    if (string.IsNullOrEmpty(@namespace))
                        @namespace = XRayAwsConventions.AttributeCloudProviderAws;
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                if (attributes.TryGetAttributeDbName(out var dbInstance))
                {
                    name = dbInstance.AsString();
                    if (attributes.TryGetAttributeDbConnectionString(out var dbUrl))
                    {
                        if (Uri.TryCreate(dbUrl.AsString(), UriKind.Absolute, out var parsed))
                        {
                            if (!string.IsNullOrEmpty(parsed.Host))
                                name += "@" + parsed.Host;
                        }
                    }
                }
            }

            var span = context.Span;
            var resourceAttributes = context.ResourceAttributes;
            if (string.IsNullOrEmpty(name) && span.Kind == ActivityKind.Server)
            {
                if (resourceAttributes.TryGetAttributeServiceName(out var serviceName))
                    name = serviceName.AsString();
            }

            if (string.IsNullOrEmpty(name))
            {
                if (attributes.TryGetAttributeRpcService(out var rpcService))
                    name = rpcService.AsString();
            }

            if (string.IsNullOrEmpty(name))
            {
                if (attributes.TryGetAttributeHttpHost(out var host))
                    name = host.AsString();
            }

            if (string.IsNullOrEmpty(name))
            {
                if (attributes.TryGetAttributeNetPeerName(out var peer))
                    name = peer.AsString();
            }

            if (string.IsNullOrEmpty(name))
                name = FixSegmentName(span.DisplayName);

            if (string.IsNullOrEmpty(@namespace) && span.Kind == ActivityKind.Client)
                @namespace = "remote";

            return (name, @namespace);
        }

        private string DetermineAwsOrigin(in XRayConverterContext context)
        {
            var resourceAttributes = context.ResourceAttributes;
            if (resourceAttributes.TryGetAttributeCloudProvider(out var provider))
            {
                if (provider.AsString() != XRayAwsConventions.AttributeCloudProviderAws)
                    return null;
            }

            if (resourceAttributes.TryGetAttributeCloudPlatform(out var platform))
            {
                switch (platform.AsString())
                {
                    case XRayAwsConventions.AttributeCloudPlatformAwsAppRunner:
                        return XRayAwsConventions.OriginAppRunner;
                    case XRayAwsConventions.AttributeCloudPlatformAwsEks:
                        return XRayAwsConventions.OriginEks;
                    case XRayAwsConventions.AttributeCloudPlatformAwsElasticBeanstalk:
                        return XRayAwsConventions.OriginElasticBeanstalk;
                    case XRayAwsConventions.AttributeCloudPlatformAwsEcs:

                        if (!resourceAttributes.TryGetAttributeAwsEcsLaunchType(out var lt))
                            return XRayAwsConventions.OriginEcs;

                        switch (lt.AsString())
                        {
                            case XRayAwsConventions.AttributeAwsEcsLaunchTypeEc2:
                                return XRayAwsConventions.OriginEcsEc2;
                            case XRayAwsConventions.AttributeAwsEcsLaunchTypeFargate:
                                return XRayAwsConventions.OriginEcsFargate;
                            default:
                                return XRayAwsConventions.OriginEcs;
                        }
                    case XRayAwsConventions.AttributeCloudPlatformAwsEc2:
                        return XRayAwsConventions.OriginEc2;

                    // If cloud_platform is defined with a non-AWS value, we should not assign it an AWS origin
                    default:
                        return null;
                }
            }

            return null;
        }

        private void WriteXRayAttributes(in XRayConverterContext context, bool storeResource)
        {
            var user = default(string);
            var spanTags = context.SpanTags;
            if (spanTags.TryGetAttributeEndUserId(out var userId))
            {
                user = userId.AsString();
                spanTags.Consume();
            }

            var writer = context.Writer;
            if (user != null)
                writer.WriteString(XRayField.User, user);

            WriteXRayMetadata(context, storeResource);
            WriteXRayAnnotations(context, storeResource);
        }

        private void WriteXRayMetadata(in XRayConverterContext context, bool storeResource)
        {
            var writer = context.Writer;

            var hasMetadata = false;
            if (storeResource)
            {
                var resourceAttributes = context.ResourceAttributes;
                foreach (var item in resourceAttributes)
                {
                    var annoVal = item.Value;
                    var indexed = _indexAllAttributes || _indexedResourceAttributes.Contains(item.Key);
                    if ((!indexed || !IsAnnotationValue(annoVal)) && IsMetadataValue(annoVal))
                    {
                        hasMetadata = WriteMetadataObject(hasMetadata, writer);

                        WriteMetadataResourceKey(writer, item.Key);
                        WriteValue(writer, annoVal);
                    }
                }
            }

            if (!_indexAllAttributes)
            {
                var spanTags = context.SpanTags;
                foreach (var (key, value) in spanTags)
                {
                    if (!_indexedAttributes.Contains(key) && IsMetadataValue(value))
                    {
                        hasMetadata = WriteMetadataObject(hasMetadata, writer);

                        writer.WritePropertyName(key);
                        WriteValue(writer, value);
                    }
                }
            }

            if (hasMetadata)
            {
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            static bool WriteMetadataObject(bool hasMetadata, Utf8JsonWriter writer)
            {
                if (!hasMetadata)
                {
                    writer.WritePropertyName(XRayField.Metadata);
                    writer.WriteStartObject();
                    writer.WritePropertyName(XRayField.Default);
                    writer.WriteStartObject();
                }

                return true;
            }
        }

        private void WriteXRayAnnotations(in XRayConverterContext context, bool storeResource)
        {
            var writer = context.Writer;
            var hasAnnotations = false;
            if (storeResource)
            {
                var resourceAttributes = context.ResourceAttributes;
                foreach (var item in resourceAttributes)
                {
                    var annoVal = item.Value;
                    var indexed = _indexAllAttributes || _indexedResourceAttributes.Contains(item.Key);
                    if (indexed && IsAnnotationValue(annoVal))
                    {
                        hasAnnotations = WriteAnnotationsObject(hasAnnotations, writer);

                        WriteAnnotationResourceKey(writer, item.Key);
                        WriteValue(writer, annoVal);
                    }
                }
            }

            if (_indexActivityNames)
            {
                hasAnnotations = WriteAnnotationsObject(hasAnnotations, writer);

                writer.WriteString("activity_display_name", context.Span.DisplayName);
                writer.WriteString("activity_operation_name", context.Span.OperationName);
            }

            var spanTags = context.SpanTags;
            foreach (var item in spanTags)
            {
                if (!_indexAllAttributes && !_indexedAttributes.Contains(item.Key))
                    continue;

                var key = FixAnnotationKey(item.Key);
                var annoVal = item.Value;
                if (IsAnnotationValue(annoVal))
                {
                    hasAnnotations = WriteAnnotationsObject(hasAnnotations, writer);

                    writer.WritePropertyName(key);
                    WriteValue(writer, annoVal);
                }
            }

            if (hasAnnotations)
                writer.WriteEndObject();

            static bool WriteAnnotationsObject(bool hasAnnotations, Utf8JsonWriter writer)
            {
                if (!hasAnnotations)
                {
                    writer.WritePropertyName(XRayField.Annotations);
                    writer.WriteStartObject();
                }

                return true;
            }
        }

        private static void WriteMetadataResourceKey(Utf8JsonWriter writer, string key)
        {
            if (key.Length > 24)
            {
                var finalKey = ResourceKeyPrefix + key;
                writer.WritePropertyName(finalKey);
            }
            else
            {
                Span<char> finalKey = stackalloc char[ResourceKeyPrefix.Length + key.Length];
                ResourceKeyPrefix.AsSpan().CopyTo(finalKey);
                key.AsSpan().CopyTo(finalKey.Slice(ResourceKeyPrefix.Length));
                writer.WritePropertyName(finalKey);
            }
        }

        private static unsafe void WriteAnnotationResourceKey(Utf8JsonWriter writer, string key)
        {
            if (key.Length > 24)
            {
                var finalKey = FixAnnotationKey(AnnotationResourceKeyPrefix + key);
                writer.WritePropertyName(finalKey);
            }
            else
            {
                Span<char> finalKey = stackalloc char[AnnotationResourceKeyPrefix.Length + key.Length];
                AnnotationResourceKeyPrefix.AsSpan().CopyTo(finalKey);
                key.AsSpan().CopyTo(finalKey.Slice(AnnotationResourceKeyPrefix.Length));

                for (var i = 0; i < finalKey.Length; i++)
                {
                    var c = finalKey[i];
                    var isValidAnnotationChar = c >= '0' && c <= '9'
                        || c >= 'a' && c <= 'z'
                        || c >= 'A' && c <= 'Z';
                    if (!isValidAnnotationChar)
                        finalKey[i] = '_';
                }

                writer.WritePropertyName(finalKey);
            }
        }

        private bool IsAnnotationValue(object value)
        {
            if (value == null)
                return false;

            // Check for supported primitive types
            if (value is string
                || value is double
                || value is long
                || value is int
                || value is bool
                || value is DateTime
                || value is Guid
                || value is float
                || value is byte
                || value is decimal
                || value is short
                || value is sbyte
                || value is ushort
                || value is uint
                || value is ulong
                || value is char
                || value is DateTimeOffset
                || value is Uri
#if NET6_0_OR_GREATER
                || value is TimeSpan
                || value is Version
                || value is JsonValue
                || value is DateOnly
                || value is TimeOnly
#endif
               )
                return true;

            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Array:
                    case JsonValueKind.Object:
                    case JsonValueKind.Undefined:
                    case JsonValueKind.Null:
                        return false;
                    default:
                        return true;
                }
            }

            var type = value.GetType();
            if (type.IsEnum)
                return true;

            return false;
        }

        private bool IsMetadataValue(object value)
        {
            if (value == null)
                return false;

            if (IsAnnotationValue(value))
                return true;

            // Should we allow any object here? Let System.Text.Json to try and serialize it?
            return value is IEnumerable // assume that system.text.json can serialize this
                || value is JsonDocument
                || value is JsonElement
#if NET6_0_OR_GREATER
                || value is JsonNode
#endif
                ;
        }

        private static void WriteValue(Utf8JsonWriter writer, object value)
        {
            JsonSerializer.Serialize(writer, value);
        }
    }
}