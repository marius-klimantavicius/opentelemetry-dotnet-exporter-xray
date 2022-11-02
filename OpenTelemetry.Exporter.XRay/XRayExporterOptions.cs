using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Amazon.XRay;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.XRay
{
    /// <summary>
    /// AWS X-Ray exporter options.
    /// </summary>
    public class XRayExporterOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to configure Activity.TraceIdGenerator to generate X-Ray compatible trace ids.
        /// If set to false consider using AddXRayTraceId from AWSXRay extensions.
        /// </summary>
        public bool GenerateTraceIds { get; set; } = true;

        /// <summary>
        /// Gets or sets a client factory for IAmazonXRay. If not provided then
        /// client will be resolved from IServiceProvider if possible or a default
        /// instance will be created otherwise.
        /// </summary>
        public Func<IAmazonXRay> AmazonXRayClientFactory { get; set; }

        /// <summary>
        /// <para>
        /// Gets or sets a list of indexed attributes to be passed as annotations to X-Ray.
        /// Resource attributes are passed with "otel.resource." prefix.
        /// </para>
        /// <para>
        /// This is ignored if <see cref="IndexAllAttributes"/> is provided.
        /// </para>
        /// </summary>
        public IEnumerable<string> IndexedAttributes { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// <para>
        /// Gets or sets the function to determine whether the attribute that is not in the <see cref="IndexedAttributes"/> list
        /// should be passed via annotations to X-Ray.
        /// If this function returns false then the attribute is passed via metadata to X-Ray.
        /// </para>
        /// <para>
        /// The first argument is the name of attributes/tag.
        /// The second argument is whether the name refers to resource attribute/tag (true) or to activity tag/attribute.
        /// If resource attribute is indexed then "otel_resource_" prefix is prepended to the annotation key.
        /// If resource attribute is not indexed then "otel.resource." prefix is prepended to the metadata key.
        /// </para>
        /// <para>
        /// This function is not invoked for attributes listed in <see cref="IndexedAttributes"/>.
        /// </para>
        /// <para>
        /// This is ignored if <see cref="IndexAllAttributes"/> is provided.
        /// </para>
        /// </summary>
        public Func<string, bool, bool> ShouldIndexAttribute { get; set; }

        /// <summary>
        /// <para>
        /// Gets or sets a value indicating whether all unknown attributes are to be passed as annotations to X-Ray.
        /// If false the attributes are passed based on <see cref="IndexedAttributes"/> and
        /// <see cref="ShouldIndexAttribute"/> options.
        /// </para>
        /// <para>
        /// This overrides <see cref="ShouldIndexAttribute"/> and <see cref="IndexedAttributes"/>.
        /// </para>
        /// </summary>
        public bool IndexAllAttributes { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to add <see cref="Activity.DisplayName"/> and <see cref="Activity.OperationName"/>
        /// as "activity_display_name" and "activity_operation_name" to annotations.
        /// </summary>
        public bool IndexActivityNames { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to validate passed trace id.
        /// If the trace id is invalid or expired then the activity will be rejected/ignored.
        /// </summary>
        public bool ValidateTraceId { get; set; } = false;

        /// <summary>
        /// Gets or sets the type of Export Processor to be used.
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Gets or sets the options for batch export processor.
        /// </summary>
        public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; } = new BatchExportActivityProcessorOptions();
    }
}
