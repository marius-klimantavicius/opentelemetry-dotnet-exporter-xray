using System;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal ref struct XRayReverseStringReader
    {
        private bool _returnEmpty;

        public ReadOnlySpan<char> Input;
        public ReadOnlySpan<char> Line;
        public ReadOnlySpan<char> Remainder;

        public int LineStart;
        public int LineEnd;

        public XRayReverseStringReader(ReadOnlySpan<char> input)
        {
            Input = input;
            Line = ReadOnlySpan<char>.Empty;
            LineStart = 0;
            LineEnd = 0;

            Remainder = input;
            _returnEmpty = input.Length > 0 && (input[0] == '\r' || input[0] == '\n');
        }

        public bool ReadLine()
        {
            if (Remainder.IsEmpty)
            {
                if (_returnEmpty)
                {
                    Line = ReadOnlySpan<char>.Empty;
                    LineStart = 0;
                    LineEnd = 0;
                    _returnEmpty = false;
                    return true;
                }

                return false;
            }

            var s = Remainder;
            var lastIndexOfNewline = s.LastIndexOfAny('\r', '\n');
            if (lastIndexOfNewline >= 0)
            {
                Line = s[(lastIndexOfNewline + 1)..];

                var ch = s[lastIndexOfNewline];
                var pos = lastIndexOfNewline;
                if (ch == '\n')
                {
                    if (pos > 0 && s[pos - 1] == '\r')
                        pos--;
                }

                LineStart = lastIndexOfNewline + 1;
                LineEnd = LineStart + Line.Length;
                Remainder = s[..pos];
            }
            else
            {
                Line = s;
                LineStart = 0;
                LineEnd = s.Length;
                Remainder = ReadOnlySpan<char>.Empty;
            }

            return true;
        }
    }
}