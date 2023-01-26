using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Amazon.XRay.Model;

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

        [NonEvent]
        public void UnprocessedTraceSegments(List<UnprocessedTraceSegment> list)
        {
            if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                var sb = new ValueStringBuilder();
                foreach (var item in list)
                {
                    if (sb.Length > 0)
                        sb.Append(", ");

                    sb.Append('{');
                    sb.Append("id: ");
                    sb.Append(item.Id);
                    sb.Append(", errorCode: ");
                    sb.Append(item.ErrorCode);
                    sb.Append(", message: ");
                    sb.Append(item.Message);
                    sb.Append("}");
                }

                UnprocessedTraceSegments(sb.ToString());
            }
        }

        [Event(2, Message = "Some activities were rejected by x-ray: {0}", Level = EventLevel.Warning)]
        public void UnprocessedTraceSegments(string errors)
        {
            WriteEvent(2, errors);
        }

        [Event(3, Message = "Received trace ID: {0} is not a valid X-Ray trace ID", Level = EventLevel.Warning)]
        public void InvalidXRayTraceId(string traceId)
        {
            if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                WriteEvent(3, traceId);
            }
        }
    }
}