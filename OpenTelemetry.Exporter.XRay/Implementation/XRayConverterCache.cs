using System.Text.Json;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal class XRayConverterCache
    {
        public readonly XRayTagDictionary ResourceAttributes;
        public readonly XRayTagDictionary SpanTags;
        public readonly Utf8JsonWriter Writer;
        public readonly PooledByteBufferWriter Buffer;
        
        public XRayConverterCache()
        {
            ResourceAttributes = new XRayTagDictionary();
            SpanTags = new XRayTagDictionary();

            Buffer = PooledByteBufferWriter.CreateEmptyInstanceForCaching();
            Writer = new Utf8JsonWriter(Buffer);
        }

        public void Return()
        {
            ResourceAttributes.Clear();
            SpanTags.Clear();
            Writer.Reset();
            Buffer.ClearAndReturnBuffers();
        }
    }
}