using System;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal ref struct XRayStringReader
    {
        private int _position;

        public ReadOnlySpan<char> Input;
        public ReadOnlySpan<char> Line;
        public ReadOnlySpan<char> Remainder => Input[_position..];

        public int LineStart;
        public int LineEnd;

        public XRayStringReader(ReadOnlySpan<char> input)
        {
            Input = input;
            Line = ReadOnlySpan<char>.Empty;
            LineStart = 0;
            LineEnd = 0;

            _position = 0;
        }

        public bool ReadLine()
        {
            if ((uint)_position >= (uint)Input.Length)
                return false;

            var s = Input[_position..];
            if (s.IsEmpty)
                return false;

            var foundLineLength = s.IndexOfAny('\r', '\n');
            if (foundLineLength >= 0)
            {
                Line = s[..foundLineLength];

                var ch = s[foundLineLength];
                var pos = foundLineLength + 1;
                if (ch == '\r')
                {
                    if ((uint)pos < (uint)s.Length && s[pos] == '\n')
                        pos++;
                }

                LineStart = _position;
                LineEnd = _position + foundLineLength;
                _position += pos;
            }
            else
            {
                Line = s;
                LineStart = _position;
                _position += s.Length;
                LineEnd = _position;
            }

            return true;
        }
    }
}