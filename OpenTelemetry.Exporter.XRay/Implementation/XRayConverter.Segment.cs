using System.Diagnostics;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private const char Version = '1';
        private const int EpochHexDigits = 8;
        private const char TraceIdDelimiter = '-';
        
        private static string ToXRayTraceIdFormat(string traceId)
        {
            var sb = new ValueStringBuilder();
            sb.Append(Version);
            sb.Append(TraceIdDelimiter);
            sb.Append(traceId.Substring(0, EpochHexDigits));
            sb.Append(TraceIdDelimiter);
            sb.Append(traceId.Substring(EpochHexDigits));

            return sb.ToString();
        }

        internal static ActivitySpanId NewSegmentId()
        {
            return ActivitySpanId.CreateRandom();
        }
    }
}