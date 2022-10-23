using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private const double TicksPerMillisecond = 10000;
        private const double TicksPerSecond = TicksPerMillisecond * 1000;
        
        private const string DefaultSegmentName = "span";
        private const int MaxSegmentNameLength = 200;

        private static readonly Regex _invalidSpanCharacters = new Regex(@"[^ 0-9\p{L}_.:/%&#=+,\\\-@]", RegexOptions.Compiled);
        private static readonly Regex _invalidAnnotationCharacters = new Regex(@"[^0-9a-zA-Z]", RegexOptions.Compiled);

        private readonly HashSet<string> _indexedAttributes;
        private readonly bool _indexAllAttributes;
        private readonly bool _indexActivityNames;
        private readonly bool _validateTraceId;

        [ThreadStatic]
        private static XRayConverterCache _cache;

        public XRayConverter(IEnumerable<string> indexedAttributes, bool indexAllAttributes, bool indexActivityNames, bool validateTraceId = false)
        {
            _indexedAttributes = new HashSet<string>(indexedAttributes ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            _indexAllAttributes = indexAllAttributes;
            _indexActivityNames = indexActivityNames;
            _validateTraceId = validateTraceId;
        }

        public string Convert(Resource resource, Activity span)
        {
            var segmentType = default(string);
            var storeResource = true;

            if (span.Kind != ActivityKind.Server
                && span.ParentSpanId.ToHexString() != "0000000000000000")
            {
                segmentType = "subsegment";
                storeResource = false;
            }

            _cache ??= new XRayConverterCache();

            try
            {
                var resourceAttributes = _cache.ResourceAttributes;
                var attributes = _cache.SpanTags;
                var writer = _cache.Writer;
                var buffer = _cache.Buffer;

                resourceAttributes.Initialize(resource.Attributes);
                attributes.Initialize(span.TagObjects);
                buffer.InitializeEmptyInstance(1024);

                var context = new XRayConverterContext(resource, resourceAttributes, span, attributes, writer);

                var (name, @namespace) = ResolveName(context);
                attributes.ResetConsume();

                var traceId = ToXRayTraceIdFormat(span.TraceId.ToString());
                if (string.IsNullOrEmpty(traceId))
                    return null;
                
                var startTime = (span.StartTimeUtc - DateTime.UnixEpoch).TotalSeconds;
                var endTime = startTime + span.Duration.TotalSeconds;

                writer.WriteStartObject();
                writer.WriteString(XRayWriter.Name, name);
                writer.WriteString(XRayWriter.Id, span.SpanId.ToHexString());
                writer.WriteString(XRayWriter.TraceId, traceId);
                writer.WriteNumber(XRayWriter.StartTime, startTime);
                writer.WriteNumber(XRayWriter.EndTime, endTime);
                if (span.ParentSpanId.ToHexString() != "0000000000000000")
                    writer.WriteString(XRayWriter.ParentId, span.ParentSpanId.ToHexString());
                if (@namespace != null)
                    writer.WriteString(XRayWriter.Namespace, @namespace);
                if (segmentType != null)
                    writer.WriteString(XRayWriter.Type, segmentType);

                WriteHttp(context);
                WriteCause(context);

                var origin = DetermineAwsOrigin(context);
                if (origin != null)
                    writer.WriteString(XRayWriter.Origin, origin);

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
                _cache.Return();
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
                writer.WriteString(XRayWriter.User, user);

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
                    var key = "otel.resource." + item.Key;
                    var annoVal = AnnotationValue(item.Value);
                    var indexed = _indexAllAttributes || _indexedAttributes.Contains(key);
                    if ((annoVal == null || !indexed) && IsMetadataValue(item.Value))
                    {
                        hasMetadata = WriteMetadataObject(hasMetadata, writer);

                        writer.WritePropertyName(key);
                        WriteValue(writer, item.Value);
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
                    writer.WritePropertyName(XRayWriter.Metadata);
                    writer.WriteStartObject();
                    writer.WritePropertyName(XRayWriter.Default);
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
                    var key = "otel.resource." + item.Key;
                    var annoVal = AnnotationValue(item.Value);
                    var indexed = _indexAllAttributes || _indexedAttributes.Contains(key);
                    if (annoVal != null && indexed)
                    {
                        hasAnnotations = WriteAnnotationsObject(hasAnnotations, writer);

                        key = FixAnnotationKey(key);
                        writer.WritePropertyName(key);
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
                var annoVal = AnnotationValue(item.Value);
                if (annoVal != null)
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
                    writer.WritePropertyName(XRayWriter.Annotations);
                    writer.WriteStartObject();
                }

                return true;
            }
        }

        private object AnnotationValue(object value)
        {
            if (value is string
                || value is int
                || value is long
                || value is double
                || value is bool)
                return value;

            return null;
        }

        private bool IsMetadataValue(object value)
        {
            if (value is string
                || value is int
                || value is long
                || value is double
                || value is bool
                || value is IDictionary
                || value is IEnumerable)
                return true;

            return false;
        }

        private static void WriteValue<T>(Utf8JsonWriter writer, T value)
        {
            switch (value)
            {
                case string stringValue:
                    writer.WriteStringValue(stringValue);
                    break;
                case int intValue:
                    writer.WriteNumberValue(intValue);
                    break;
                case long longValue:
                    writer.WriteNumberValue(longValue);
                    break;
                case double doubleValue:
                    writer.WriteNumberValue(doubleValue);
                    break;
                case bool boolValue:
                    writer.WriteBooleanValue(boolValue);
                    break;
                case IDictionary dictionary:
                    writer.WriteStartObject();

                    foreach (DictionaryEntry item in dictionary)
                    {
                        if (item.Key is string stringKey)
                        {
                            writer.WritePropertyName(stringKey);
                            WriteValue(writer, item.Value);
                        }
                    }

                    writer.WriteEndObject();
                    break;
                
                case string[] array:
                    WriteArray(writer, array);
                    break;
                
                case bool[] array:
                    WriteArray(writer, array);
                    break;
                
                case double[] array:
                    WriteArray(writer, array);
                    break;

                case long[] array:
                    WriteArray(writer, array);
                    break;

                case IEnumerable enumerable:
                    writer.WriteStartArray();

                    foreach (var item in enumerable)
                        WriteValue(writer, item);

                    writer.WriteEndArray();
                    break;
            }

            static void WriteArray<TArrayItem>(Utf8JsonWriter writer, TArrayItem[] array)
            {
                writer.WriteStartArray();

                foreach (var item in array)
                    WriteValue(writer, item);

                writer.WriteEndArray();
            }
        }
    }
}