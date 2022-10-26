using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private const char Version = '1';
        private const int EpochHexDigits = 8;
        private const char TraceIdDelimiter = '-';

        private const int MaxAge = 60 * 60 * 24 * 28;
        private const int MaxSkew = 60 * 5;

        private static ReadOnlySpan<byte> CharToHexLookup => new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
            0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
            0xFF, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
            0xFF, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 111
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 127
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 143
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 159
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 175
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 191
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 207
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 223
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 239
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 255
        };

        internal static string FixSegmentName(string name)
        {
            name = _invalidSpanCharacters.Replace(name, "");
            if (name.Length > MaxSegmentNameLength)
                name = name.Substring(0, MaxSegmentNameLength);
            else if (name.Length == 0)
                name = DefaultSegmentName;

            return name;
        }

        internal static string FixAnnotationKey(string name)
        {
            return _invalidAnnotationCharacters.Replace(name, "_");
        }

        private unsafe bool IsValidXRayTraceId(ReadOnlySpan<char> traceId)
        {
            if (!_validateTraceId)
                return true;

            if (traceId.Length < 32)
                return false;
            
            var epoch = traceId.Slice(0, EpochHexDigits);
            var epochValue = default(int);
            var epochSpan = new Span<byte>(&epochValue, sizeof(int));
            if (!TryDecodeHex(epoch, epochSpan))
                return false;

            if (BitConverter.IsLittleEndian)
                epochValue = BinaryPrimitives.ReverseEndianness(epochValue);

            var epochNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var delta = epochNow - epochValue;
            return delta <= MaxAge && delta >= -MaxSkew;
        }

        private unsafe void WriteXRayTraceId(Utf8JsonWriter writer, ReadOnlySpan<char> traceId)
        {
            writer.WritePropertyName(XRayField.TraceId);

            if (traceId.Length < EpochHexDigits) // should not happen
            {
                writer.WriteStringValue(traceId);
                return;
            }

            var epoch = traceId.Slice(0, EpochHexDigits);

            // This should always be 32
            if (traceId.Length <= 32)
            {
                Span<char> result = stackalloc char[traceId.Length + 3];
                result[EpochHexDigits + 2] = TraceIdDelimiter;
                result[0] = Version;
                result[1] = TraceIdDelimiter;
                epoch.CopyTo(result.Slice(2));
                traceId.Slice(EpochHexDigits).CopyTo(result.Slice(EpochHexDigits + 3));
                writer.WriteStringValue(result);
            }
            else
            {
                var sb = new ValueStringBuilder();
                sb.Append(Version);
                sb.Append(TraceIdDelimiter);
                sb.Append(epoch);
                sb.Append(TraceIdDelimiter);
                sb.Append(traceId.Slice(EpochHexDigits));
                writer.WriteStringValue(sb.AsSpan());
                sb.Dispose();
            }
        }

        private static ActivitySpanId NewSegmentId()
        {
            return ActivitySpanId.CreateRandom();
        }

        private static bool TryDecodeHex(ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            var i = 0;
            var j = 0;
            var byteLo = 0;
            var byteHi = 0;
            while (j < bytes.Length)
            {
                byteLo = FromChar(chars[i + 1]);
                byteHi = FromChar(chars[i]);

                // byteHi hasn't been shifted to the high half yet, so the only way the bitwise or produces this pattern
                // is if either byteHi or byteLo was not a hex character.
                if ((byteLo | byteHi) == 0xFF)
                    break;

                bytes[j++] = (byte)((byteHi << 4) | byteLo);
                i += 2;
            }

            return (byteLo | byteHi) != 0xFF;
        }

        private static int FromChar(int c)
        {
            return c >= CharToHexLookup.Length ? 0xFF : CharToHexLookup[c];
        }
    }
}