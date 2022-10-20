using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal static class XRayTraceId
    {
        public static unsafe ActivityTraceId Generate()
        {
            var random = RandomNumberGenerator.Current;
            Span<byte> span = stackalloc byte[2 * sizeof(long)];

            Unsafe.WriteUnaligned(ref span[0], random.Next());
            Unsafe.WriteUnaligned(ref span[8], random.Next());

            var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            if (BitConverter.IsLittleEndian)
                now = BinaryPrimitives.ReverseEndianness(now);

            Unsafe.WriteUnaligned(ref span[0], now);

            return ActivityTraceId.CreateFromBytes(span);
        }
    }
}