using System;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal ref struct XRayStringReader
    {
        public ReadOnlySpan<char> Input;
        public ReadOnlySpan<char> Line;

        public int Position;
        public int LineStart;
        public int LineEnd;
        
        public XRayStringReader(ReadOnlySpan<char> input)
        {
            Input = input;
            Line = ReadOnlySpan<char>.Empty;
            Position = 0;
            LineStart = 0;
            LineEnd = 0;
        }
        
        public bool ReadLine()
        {
            if ((uint)Position >= (uint)Input.Length)
                return false;
            
            var s = Input[Position..];
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

                LineStart = Position;
                LineEnd = Position + foundLineLength;
                Position += pos;
            }
            else
            {
                Line = s;
                LineStart = Position;
                Position += s.Length;
                LineEnd = Position;
            }

            return true;
        }
    }
}