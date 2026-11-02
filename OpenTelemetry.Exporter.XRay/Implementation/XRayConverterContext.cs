using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal readonly struct XRayConverterContext
    {
        public readonly Resource Resource;
        public readonly XRayTagDictionary ResourceAttributes;

        public readonly Activity Span;
        public readonly XRayTagDictionary SpanTags;

        public readonly Utf8JsonWriter Writer;
        public readonly bool IsServiceSegment;

        public XRayConverterContext(Resource resource, XRayTagDictionary resourceAttributes, Activity span, XRayTagDictionary spanTags, Utf8JsonWriter writer, bool isServiceSegment = false)
        {
            Writer = writer;
            Resource = resource;
            ResourceAttributes = resourceAttributes;
            Span = span;
            SpanTags = spanTags;
            IsServiceSegment = isServiceSegment;
        }
    }
}