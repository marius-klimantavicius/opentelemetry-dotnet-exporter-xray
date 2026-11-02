using System.Collections;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private void WriteService(in XRayConverterContext context)
        {
            var attributes = context.ResourceAttributes;
            var hasVersion = attributes.TryGetAttributeServiceVersion(out var version);
            if (!hasVersion)
            {
                if (attributes.TryGetAttributeContainerImageTags(out var tags) && tags is IList values && values.Count > 0)
                {
                    version = values[0];
                    hasVersion = true;
                }
                else
                {
                    hasVersion = attributes.TryGetAttributeContainerImageTag(out version);
                }
            }

            if (hasVersion)
            {
                var writer = context.Writer;
                writer.WritePropertyName(XRayField.Service);
                writer.WriteStartObject();

                if (version.AsString() != null)
                    writer.WriteString(XRayField.Version, version.AsString());

                writer.WriteEndObject();
            }
        }
    }
}