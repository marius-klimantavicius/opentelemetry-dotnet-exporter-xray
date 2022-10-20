namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal readonly struct XRayHttpUrlPartValue
    {
        public readonly string Value;
        public readonly bool HasValue;

        public XRayHttpUrlPartValue(string value)
        {
            Value = value;
            HasValue = true;
        }

        public bool TryGetValue(out string result)
        {
            result = Value;
            return HasValue;
        }

        public static implicit operator XRayHttpUrlPartValue(string value)
        {
            return new XRayHttpUrlPartValue(value);
        }

        public static implicit operator string(XRayHttpUrlPartValue value)
        {
            return value.Value;
        }
    }
}