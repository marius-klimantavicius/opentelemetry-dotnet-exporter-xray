namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private void WriteService(in XRayConverterContext context)
        {
            if (context.ResourceAttributes.TryGetAttributeServiceVersion(out var version) ||
                context.ResourceAttributes.TryGetAttributeContainerImageTag(out version))
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