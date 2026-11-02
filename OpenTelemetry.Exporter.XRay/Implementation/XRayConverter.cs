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
        private const string MetadataKeyPrefix = "aws.xray.metadata.";
        private const int MaxSegmentNameLength = 200;

        private static readonly Regex _invalidSpanCharacters = new Regex(@"[^ 0-9\p{L}_.:/%&#=+\\\-@]", RegexOptions.Compiled);
        private static readonly Regex _invalidAnnotationCharacters = new Regex(@"[^0-9a-zA-Z]", RegexOptions.Compiled);
        private static readonly Regex _invalidAnnotationCharactersAllowDot = new Regex(@"[^0-9a-zA-Z.]", RegexOptions.Compiled);

        private readonly Func<string, bool, bool> _shouldIndexAttribute;
        private readonly bool _indexAllAttributes;
        private readonly bool _indexActivityNames;
        private readonly IEnumerable<string> _logGroupNames;
        private readonly bool _validateTraceId;
        private readonly bool _overwriteInvalidTraceId;
        private readonly bool _allowDotInAnnotationKey;
        private readonly HashSet<string> _indexedAttributes;
        private readonly bool _emitV1DbConventions;
        private readonly bool _dontEmitV0DbConventions;
        private readonly bool _emitV1HttpNetworkConventions;
        private readonly bool _dontEmitV0HttpNetworkConventions;

        [ThreadStatic]
        private static XRayConverterCache _cache;

        public XRayConverter(Func<string, bool, bool> shouldIndexAttribute,
            IEnumerable<string> indexedAttributes,
            bool indexAllAttributes,
            bool indexActivityNames,
            bool validateTraceId = false,
            bool overwriteInvalidTraceId = false,
            IEnumerable<string> logGroupNames = null,
            bool allowDotInAnnotationKey = false,
            bool emitV1DbConventions = false,
            bool dontEmitV0DbConventions = false,
            bool emitV1HttpNetworkConventions = false,
            bool dontEmitV0HttpNetworkConventions = false)
        {
            ValidateConventions(emitV1DbConventions, dontEmitV0DbConventions, emitV1HttpNetworkConventions, dontEmitV0HttpNetworkConventions);
            _indexedAttributes = new HashSet<string>(indexedAttributes ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            _indexAllAttributes = indexAllAttributes;
            _shouldIndexAttribute = GenerateShouldIndexAttribute(shouldIndexAttribute, _indexedAttributes, false);
            _indexActivityNames = indexActivityNames;
            _logGroupNames = logGroupNames;
            _allowDotInAnnotationKey = allowDotInAnnotationKey;
            _validateTraceId = validateTraceId;
            _overwriteInvalidTraceId = overwriteInvalidTraceId;
            _emitV1DbConventions = emitV1DbConventions;
            _dontEmitV0DbConventions = dontEmitV0DbConventions;
            _emitV1HttpNetworkConventions = emitV1HttpNetworkConventions;
            _dontEmitV0HttpNetworkConventions = dontEmitV0HttpNetworkConventions;
        }

        public XRayConverter(Func<string, bool, bool> shouldIndexAttribute,
            bool indexAllAttributes,
            bool indexActivityNames,
            bool validateTraceId = false,
            bool overwriteInvalidTraceId = false,
            IEnumerable<string> logGroupNames = null,
            bool allowDotInAnnotationKey = false,
            bool emitV1DbConventions = false,
            bool dontEmitV0DbConventions = false,
            bool emitV1HttpNetworkConventions = false,
            bool dontEmitV0HttpNetworkConventions = false)
            : this(
                shouldIndexAttribute,
                null,
                indexAllAttributes,
                indexActivityNames,
                validateTraceId,
                overwriteInvalidTraceId,
                logGroupNames,
                allowDotInAnnotationKey,
                emitV1DbConventions,
                dontEmitV0DbConventions,
                emitV1HttpNetworkConventions,
                dontEmitV0HttpNetworkConventions)
        {
        }

        internal static void ValidateConventions(bool emitV1DBConventions, bool dontEmitV0DBConventions, bool emitV1HTTPNetworkConventions, bool dontEmitV0HTTPNetworkConventions)
        {
            if (dontEmitV0DBConventions && !emitV1DBConventions)
                throw new ArgumentException("DontEmitV0DBConventions requires EmitV1DBConventions.", nameof(dontEmitV0DBConventions));
            if (dontEmitV0HTTPNetworkConventions && !emitV1HTTPNetworkConventions)
                throw new ArgumentException("DontEmitV0HTTPNetworkConventions requires EmitV1HTTPNetworkConventions.", nameof(dontEmitV0HTTPNetworkConventions));
        }

        // Retain the original single-document API. The exporter uses ConvertDocuments for local roots.
        public string Convert(Resource resource, Activity span)
        {
            var traceId = ResolveTraceId(span);
            return traceId == null ? null : ConvertCore(resource, span, traceId);
        }

        public (string first, string second) ConvertDocuments(Resource resource, Activity span)
        {
            var traceId = ResolveTraceId(span);
            if (traceId == null)
                return default;

            var localRoot = span.GetTagItem(XRayConventions.AttributeAwsSpanKind) as string == "LOCAL_ROOT";
            if (!localRoot)
                return (ConvertCore(resource, span, traceId), null);

            if (span.Kind == ActivityKind.Server || span.Kind == ActivityKind.Internal)
                return (ConvertCore(resource, span, traceId, localRoot: true), null);

            var serviceId = NewSegmentId().ToHexString();
            var dependency = ConvertCore(resource, span, traceId, serviceId, localRoot: true);
            var service = ConvertCore(resource, span, traceId, serviceId, localRoot: true, serviceSegment: true);
            return (dependency, service);
        }

        private string ResolveTraceId(Activity span)
        {
            var traceId = span.TraceId.ToHexString();
            if (!IsValidXRayTraceId(traceId))
            {
                XRayExporterEventSource.Log.InvalidXRayTraceId(traceId);
                if (!_overwriteInvalidTraceId)
                    return null;

                traceId = XRayTraceId.Generate().ToHexString();
            }

            // An invalid link rejects the whole span, as in the upstream translator. Rewriting a
            // linked trace ID would break its relationship to the linked trace.
            foreach (var link in span.Links)
            {
                var linkedTraceId = link.Context.TraceId.ToHexString();
                if (!IsValidXRayTraceId(linkedTraceId))
                {
                    XRayExporterEventSource.Log.InvalidXRayTraceId(linkedTraceId);
                    return null;
                }
            }

            return traceId;
        }

        private static bool IsRemoteServiceAttribute(string key)
        {
            return key == XRayConventions.AttributeAwsRemoteService
                || key == XRayConventions.AttributeAwsRemoteOperation
                || key == XRayConventions.AttributeRemoteTarget
                || key == XRayConventions.AttributeK8SRemoteNamespace;
        }

        private string ConvertCore(Resource resource, Activity span, string traceId, string serviceId = null, bool localRoot = false, bool serviceSegment = false)
        {
            var parentId = serviceId != null && !serviceSegment ? serviceId : span.ParentSpanId.ToHexString();
            var subsegment = span.Kind != ActivityKind.Server && span.Kind != ActivityKind.Consumer && parentId != "0000000000000000";

            // Resource placement follows the input kind and parent, before local-root type overrides.
            var storeResource = !subsegment;
            var cache = _cache ?? new XRayConverterCache();

            // Borrow exclusively: callbacks or serializers can reenter the converter on this thread.
            _cache = null;
            try
            {
                var resourceAttributes = cache.ResourceAttributes;
                var attributes = cache.SpanTags;
                var writer = cache.Writer;
                var buffer = cache.Buffer;
                var originalTags = span.TagObjects;

                resourceAttributes.Initialize(resource.Attributes);

                // ReSharper disable once PossibleMultipleEnumeration
                attributes.Initialize(originalTags);
                if (serviceSegment)
                {
                    // Consume
                    attributes.TryGetAttributeAwsRemoteService(out _);
                    attributes.TryGetAttributeAwsRemoteOperation(out _);
                    attributes.TryGetAttributeRemoteTarget(out _);
                    attributes.TryGetAttributeK8SRemoteNamespace(out _);
                    attributes.Consume();
                }

                buffer.InitializeEmptyInstance(1024);

                var context = new XRayConverterContext(resource, resourceAttributes, span, attributes, writer, serviceSegment);
                var (name, @namespace) = ResolveName(context);
                if (serviceSegment)
                {
                    var localName = span.GetTagItem(XRayConventions.AttributeAwsLocalService) as string;
                    if (localName == null && resourceAttributes.TryGetAttributeServiceName(out var resourceName))
                        localName = resourceName as string;
                    name = localName ?? name;
                }

                if (localRoot)
                {
                    subsegment = serviceId != null && !serviceSegment;
                    @namespace = subsegment ? @namespace ?? "remote" : null;
                }

                attributes.ResetConsume();
                var startTime = (span.StartTimeUtc - DateTime.UnixEpoch).TotalSeconds;

                writer.WriteStartObject();
                writer.WriteString(XRayField.Name, name);
                writer.WriteString(XRayField.Id, serviceSegment ? serviceId : span.SpanId.ToHexString());
                WriteXRayTraceId(writer, traceId);
                writer.WriteNumber(XRayField.StartTime, startTime);

                if (attributes.TryGetAttributeAwsXRayInProgress(out var inProgress) && inProgress is bool inProgressValue)
                {
                    attributes.Consume();
                    if (inProgressValue)
                        writer.WriteBoolean(XRayField.InProgress, true);
                    else
                        writer.WriteNumber(XRayField.EndTime, startTime + span.Duration.TotalSeconds);
                }
                else
                {
                    attributes.ResetConsume();
                    writer.WriteNumber(XRayField.EndTime, startTime + span.Duration.TotalSeconds);
                }

                if (parentId != "0000000000000000")
                    writer.WriteString(XRayField.ParentId, parentId);
                if (@namespace != null)
                    writer.WriteString(XRayField.Namespace, @namespace);
                if (subsegment)
                    writer.WriteString(XRayField.Type, "subsegment");

                WriteHttp(context);
                WriteCause(context);

                var origin = DetermineAwsOrigin(context);
                if (origin != null)
                    writer.WriteString(XRayField.Origin, origin);

                WriteAws(context);
                WriteService(context);
                WriteSql(context);

                // ReSharper disable once PossibleMultipleEnumeration
                WriteXRayAttributes(context, storeResource, originalTags);

                if (!localRoot || serviceId == null || serviceSegment == (span.Kind == ActivityKind.Consumer))
                    WriteLinks(context);

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

        private void WriteLinks(in XRayConverterContext context)
        {
            var writer = context.Writer;
            var started = false;
            foreach (var link in context.Span.Links)
            {
                if (!started)
                {
                    writer.WriteStartArray(XRayField.Links);
                    started = true;
                }

                writer.WriteStartObject();
                WriteXRayTraceId(writer, link.Context.TraceId.ToHexString());
                writer.WriteString(XRayField.Id, link.Context.SpanId.ToHexString());

                if (link.Tags != null && link.Tags.Any())
                {
                    writer.WriteStartObject(XRayField.Attributes);
                    foreach (var tag in link.Tags)
                    {
                        writer.WritePropertyName(tag.Key);
                        WriteValue(writer, tag.Value);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            if (started)
                writer.WriteEndArray();
        }

        private (string name, string @namespace) ResolveName(in XRayConverterContext context)
        {
            var name = default(string);
            var @namespace = default(string);

            var attributes = context.SpanTags;
            var span = context.Span;
            var isLocalRoot = attributes.TryGetAttributeAwsSpanKind(out var awsSpanKind) && awsSpanKind.AsString() == "LOCAL_ROOT";
            if (span.Kind == ActivityKind.Server || span.Kind == ActivityKind.Internal && isLocalRoot)
            {
                if (attributes.TryGetAttributeAwsLocalService(out var localService))
                    name = localService.AsString();
            }

            if (span.Kind == ActivityKind.Client || span.Kind == ActivityKind.Producer || span.Kind == ActivityKind.Consumer)
            {
                if (attributes.TryGetAttributeAwsRemoteService(out var remoteService))
                {
                    name = remoteService.AsString();
                    if (attributes.TryGetAttributeRpcSystem(out var system) && system.AsString() == "aws-api")
                    {
                        if (name.StartsWith("AWS.SDK.", StringComparison.Ordinal))
                            name = name.Substring(8);
                        else if (name.StartsWith("AWS::", StringComparison.Ordinal))
                            name = name.Substring(5);
                    }
                }
            }

            if (string.IsNullOrEmpty(name) && attributes.TryGetAttributePeerService(out var peerService))
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
                if (_emitV1DbConventions && attributes.TryGetAttributeDbNamespace(out var dbNamespace))
                {
                    name = dbNamespace.AsString();
                    if (!string.IsNullOrEmpty(name) && attributes.TryGetAttributeServerAddress(out var address) && !string.IsNullOrEmpty(address.AsString()))
                        name += "@" + address.AsString();
                }

                if (string.IsNullOrEmpty(name) && !_dontEmitV0DbConventions && attributes.TryGetAttributeDbName(out var dbInstance))
                {
                    name = dbInstance.AsString();
                    if (attributes.TryGetAttributeDbConnectionString(out var dbUrl))
                    {
                        var url = dbUrl.AsString();
                        if (url.StartsWith("jdbc:", StringComparison.Ordinal))
                            url = url.Substring(5);

                        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
                        {
                            if (!string.IsNullOrEmpty(parsed.Host))
                                name += "@" + parsed.Host;
                        }
                    }
                }
            }

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
                if (!_dontEmitV0HttpNetworkConventions && attributes.TryGetAttributeHttpHost(out var host))
                    name = host.AsString();

                if (string.IsNullOrEmpty(name) && _emitV1HttpNetworkConventions && attributes.TryGetAttributeServerAddress(out var address))
                    name = address.AsString();
            }

            if (string.IsNullOrEmpty(name))
            {
                if (!_dontEmitV0HttpNetworkConventions && attributes.TryGetAttributeNetPeerName(out var peer))
                    name = peer.AsString();
            }

            if (string.IsNullOrEmpty(name))
                name = FixSegmentName(span.DisplayName);

            if (string.IsNullOrEmpty(@namespace) && (span.Kind == ActivityKind.Client || span.Kind == ActivityKind.Producer))
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

        private void WriteXRayAttributes(in XRayConverterContext context, bool storeResource, IEnumerable<KeyValuePair<string, object>> originalTags)
        {
            var writer = context.Writer;
            var spanTags = context.SpanTags;
            if (spanTags.TryGetAttributeEndUserId(out var userId))
            {
                writer.WriteString(XRayField.User, userId.AsString());
                spanTags.Consume();
            }

            var selected = default(HashSet<string>);
            if (spanTags.TryGetAttributeAwsXRayAnnotations(out var selection) && selection is IList list)
            {
                selected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in list)
                {
                    if (item is string key)
                        selected.Add(key);
                }

                spanTags.Consume();
            }
            else
            {
                spanTags.ResetConsume();
            }

            var annotations = new Dictionary<string, object>(StringComparer.Ordinal);
            var defaultMetadata = new Dictionary<string, object>(StringComparer.Ordinal);
            var metadata = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            if (storeResource)
            {
                foreach (var item in context.ResourceAttributes)
                {
                    var indexed = _indexAllAttributes || selected != null && selected.Contains(ResourceKeyPrefix + item.Key) || _shouldIndexAttribute(item.Key, true);
                    if (indexed && IsAnnotationValue(item.Value))
                        annotations[FixAnnotationKey(ResourceKeyPrefix + item.Key)] = item.Value;
                    else if (IsMetadataValue(item.Value))
                        defaultMetadata[ResourceKeyPrefix + item.Key] = item.Value;
                }
            }

            if (_indexActivityNames)
            {
                annotations["activity_display_name"] = context.Span.DisplayName;
                annotations["activity_operation_name"] = context.Span.OperationName;
            }

            foreach (var item in spanTags)
            {
                var indexed = _indexAllAttributes || selected != null && selected.Contains(item.Key) || _shouldIndexAttribute(item.Key, false);
                if (indexed)
                {
                    if (IsAnnotationValue(item.Value))
                        annotations[FixAnnotationKey(item.Key)] = item.Value;
                }
                else if (item.Key.StartsWith(MetadataKeyPrefix, StringComparison.Ordinal) && item.Value is string json)
                {
                    var ns = item.Key.Substring(MetadataKeyPrefix.Length);
                    try
                    {
                        using var parsed = JsonDocument.Parse(json);
                        if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                        {
                            defaultMetadata[item.Key] = item.Value;
                            continue;
                        }

                        var target = string.Equals(ns, "default", StringComparison.OrdinalIgnoreCase)
                            ? defaultMetadata
                            : new Dictionary<string, object>(StringComparer.Ordinal);
                        
                        foreach (var property in parsed.RootElement.EnumerateObject())
                            target[property.Name] = property.Value.Clone();
                        
                        if (!ReferenceEquals(target, defaultMetadata))
                            metadata[ns] = target;
                    }
                    catch (JsonException)
                    {
                        defaultMetadata[item.Key] = item.Value;
                    }
                }
                else if (IsMetadataValue(item.Value))
                {
                    defaultMetadata[item.Key] = item.Value;
                }
            }

            // Only explicitly configured keys restore consumed fields. IndexAllAttributes and
            // ShouldIndexAttribute retain their existing behavior for unconsumed attributes.
            foreach (var item in originalTags)
            {
                if (context.IsServiceSegment && IsRemoteServiceAttribute(item.Key))
                    continue;
                
                if (item.Key == XRayConventions.AttributeEndUserId 
                    || item.Key == XRayConventions.AttributeAwsXRayAnnotations
                    || item.Key == XRayConventions.AttributeAwsXRayInProgress && item.Value is bool)
                    continue;

                if (_indexedAttributes.Contains(item.Key) && IsAnnotationValue(item.Value))
                    annotations[FixAnnotationKey(item.Key)] = item.Value;
            }

            if (defaultMetadata.Count > 0)
                metadata["default"] = defaultMetadata;

            if (metadata.Count > 0)
            {
                writer.WritePropertyName(XRayField.Metadata);
                if (context.IsServiceSegment)
                {
                    writer.WriteStartObject();
                    foreach (var ns in metadata)
                    {
                        writer.WriteStartObject(ns.Key);
                        foreach (var entry in ns.Value)
                        {
                            if (!entry.Key.StartsWith(ResourceKeyPrefix, StringComparison.Ordinal))
                                continue;

                            writer.WritePropertyName(entry.Key);
                            WriteValue(writer, entry.Value);
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }
                else
                {
                    WriteValue(writer, metadata);
                }
            }

            if (annotations.Count > 0)
            {
                writer.WritePropertyName(XRayField.Annotations);
                WriteValue(writer, annotations);
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

        private static Func<string, bool, bool> GenerateShouldIndexAttribute(Func<string, bool, bool> shouldIndexAttribute, IEnumerable<string> indexedAttributes, bool indexAllAttributes)
        {
            if (indexAllAttributes)
                return null;

            var indexedAttributeSet = new HashSet<string>(indexedAttributes ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var indexedResourceAttributes = new HashSet<string>(
                (indexedAttributes ?? Enumerable.Empty<string>())
                .Where(s => s.StartsWith(ResourceKeyPrefix, StringComparison.Ordinal))
                .Select(s => s.Substring(ResourceKeyPrefix.Length)),
                StringComparer.Ordinal);

            if (indexedAttributeSet.Count > 0)
            {
                if (shouldIndexAttribute == null)
                    return (name, isResource) => isResource ? indexedResourceAttributes.Contains(name) : indexedAttributeSet.Contains(name);

                return (name, isResource) =>
                {
                    if (isResource ? indexedResourceAttributes.Contains(name) : indexedAttributeSet.Contains(name))
                        return true;

                    return shouldIndexAttribute(name, isResource);
                };
            }

            if (shouldIndexAttribute != null)
                return shouldIndexAttribute;

            return (_, __) => false;
        }
    }
}