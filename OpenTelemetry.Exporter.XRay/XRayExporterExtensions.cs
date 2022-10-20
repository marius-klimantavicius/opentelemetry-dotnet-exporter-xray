using System;
using System.Diagnostics;
using Amazon.XRay;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.XRay
{
    /// <summary>
    /// Extension methods to simplify registering a AWS X-Ray exporter.
    /// </summary>
    public static class XRayExporterExtensions
    {
        /// <summary>
        /// Registers a AWS X-Ray exporter that will receive <see cref="System.Diagnostics.Activity"/> instances.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Configure AWS X-Ray exporter options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddXRayExporter(this TracerProviderBuilder builder, Action<XRayExporterOptions> configure = null)
        {
            var options = new XRayExporterOptions();
            configure?.Invoke(options);

            if (options.GenerateTraceIds)
            {
                Activity.TraceIdGenerator = XRayTraceId.Generate;
            }

            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure((sp, theBuilder) =>
                {
                    options.AmazonXRayClientFactory ??= () => (IAmazonXRay)sp.GetService(typeof(IAmazonXRay)) ?? new AmazonXRayClient();
                    AddXRayExporter(theBuilder, options);
                });
            }

            options.AmazonXRayClientFactory ??= () => new AmazonXRayClient();
            return AddXRayExporter(builder, options);
        }

        private static TracerProviderBuilder AddXRayExporter(TracerProviderBuilder builder, XRayExporterOptions options)
        {
            var exporter = new XRayExporter(options);
            if (options.ExportProcessorType == ExportProcessorType.Simple)
            {
                return builder.AddProcessor(new SimpleActivityExportProcessor(exporter));
            }

            return builder.AddProcessor(new BatchActivityExportProcessor(
                exporter,
                options.BatchExportProcessorOptions.MaxQueueSize,
                options.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                options.BatchExportProcessorOptions.MaxExportBatchSize));
        }
    }
}
