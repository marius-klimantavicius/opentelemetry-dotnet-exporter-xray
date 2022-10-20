using System.Collections.Generic;
using System.Globalization;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal static class XRayTagValueExtensions
    {
        public static bool TryGetValue(this IEnumerable<KeyValuePair<string, object>> attributes, string key, out object value)
        {
            foreach (var item in attributes)
            {
                if (item.Key == key)
                {
                    value = item.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        public static string AsString(this object value)
        {
            if (value is string stringValue)
                return stringValue;

            return "";
        }

        public static long AsInt(this object value)
        {
            if (value is int intValue)
                return intValue;

            if (value is uint uintValue)
                return uintValue;
            
            if (value is long longValue)
                return longValue;

            return 0;
        }

        public static string HexString(this ulong value)
        {
            return value.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}