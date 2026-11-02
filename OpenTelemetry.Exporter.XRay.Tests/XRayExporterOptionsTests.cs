using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayExporterOptionsTests
    {
        public static IEnumerable<object[]> ConventionOptions()
        {
            for (var bits = 0; bits < 16; bits++)
                yield return new object[] { (bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0 };
        }

        [Fact]
        public void Defaults_preserve_local_behavior_and_keep_new_gates_opt_in()
        {
            var options = new XRayExporterOptions();
            Assert.True(options.IndexAllAttributes);
            Assert.True(options.GenerateTraceIds);
            Assert.False(options.ValidateTraceId);
            Assert.False(options.EmitV1DbConventions);
            Assert.False(options.DontEmitV0DbConventions);
            Assert.False(options.EmitV1HttpNetworkConventions);
            Assert.False(options.DontEmitV0HttpNetworkConventions);
        }

        [Theory]
        [MemberData(nameof(ConventionOptions))]
        public void Constructor_and_exporter_validate_all_gate_combinations_before_creating_client(bool db, bool noDb, bool network, bool noNetwork)
        {
            var invalid = noDb && !db || noNetwork && !network;
            var called = false;
            var options = new XRayExporterOptions
            {
                EmitV1DbConventions = db, DontEmitV0DbConventions = noDb,
                EmitV1HttpNetworkConventions = network, DontEmitV0HttpNetworkConventions = noNetwork,
                AmazonXRayClientFactory = () =>
                {
                    called = true;
                    return null;
                },
            };
            if (invalid)
            {
                Assert.Throws<ArgumentException>((Action)Construct);
                Assert.Throws<ArgumentException>((Action)Alternate);
                var error = Assert.Throws<ArgumentException>(() => new XRayExporter(options));
                Assert.Contains("requires", error.Message);
                Assert.False(called);
            }
            else
            {
                Construct();
                Alternate();
                using var exporter = new XRayExporter(options);
                Assert.True(called);
            }

            return;

            void Construct() => _ = new XRayConverter(null, null, false, false, emitV1DbConventions: db, dontEmitV0DbConventions: noDb, emitV1HttpNetworkConventions: network, dontEmitV0HttpNetworkConventions: noNetwork);
            void Alternate() => _ = new XRayConverter(null, false, false, emitV1DbConventions: db, dontEmitV0DbConventions: noDb, emitV1HttpNetworkConventions: network, dontEmitV0HttpNetworkConventions: noNetwork);
        }

        [Fact]
        public void Public_registration_rejects_invalid_options()
        {
            var originalGenerator = Activity.TraceIdGenerator;
            try
            {
                Assert.Throws<ArgumentException>(() => Sdk.CreateTracerProviderBuilder().AddXRayExporter(options =>
                {
                    options.DontEmitV0DbConventions = true;
                    options.AmazonXRayClientFactory = () => throw new InvalidOperationException("Client must not be constructed");
                }).Build());
            }
            finally
            {
                Activity.TraceIdGenerator = originalGenerator;
            }
        }
    }
}