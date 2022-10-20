using System;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    [EventSource(Name = "OpenTelemetry-Exporter-XRay")]
    internal class XRayExporterEventSource : EventSource
    {
        public static readonly XRayExporterEventSource Log = new XRayExporterEventSource();

        [NonEvent]
        public void FailedExport(Exception ex)
        {
            if (IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                FailedExport(ex.ToString());
            }
        }

        [Event(1, Message = "Failed to export activities: '{0}'", Level = EventLevel.Error)]
        public void FailedExport(string exception)
        {
            WriteEvent(1, exception);
        }
    }
}