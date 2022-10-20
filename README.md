# AWS X-Ray Exporter for OpenTelemetry .NET

**NOTE: This exporter is not affiliated with or officially supported by Amazon.**

This is a port of [AWS X-Ray Tracing Exporter for OpenTelemetry Collector](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/awsxrayexporter).

The XRay Exporter exports telemetry to AWS X-Ray service without the need for X-Ray daemon or AWS Distro for Open Telemetry.

## Installation

```shell
dotnet add package Marius.OpenTelemetry.Exporter.XRay
```
## Traces

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetSampler(new AlwaysOnSampler())
    .AddSource("DemoSource")
    .AddXRayExporter()
    .Build();
}
```

XRay exporter requires X-Ray compatible trace ids thus overwrites `Activity.TraceIdGenerator`
with a compatible generator. If `OpenTelemetry.Contrib.Extensions.AWSXRay` is used to generate
trace ids then set `XRayExporterOptions.GenerateTraceIds = false`.

### XRayExporterOptions

`XRayExporterOptions` contains various options to configure the XRay Exporter.

#### `AmazonXRayClientFactory` (optional, default: `null`)

If client factory is not provided then default will be used:
1. If trace provider builder supports dependency injection then the exporter
will try to resolve `IAmazonXRay` from service provider
2. Otherwise a new instance of `AmazonXRayClient` is created

#### `GenerateTraceIds` (optional, default: `true`)

If `true` then default trace ids generator will be used.
The value `false` should be only used if `OpenTelemetry.Contrib.Extensions.AWSXRay`
is configured to generate trace ids.

#### `IndexAllAttribute` (optional, default: `true`), `IndexedAttributes` (optional, default: empty)

If `IndexAllAttributes` is set to `true` the all non-XRay tags and attributes are passed as annotations
for AWS X-Ray service to be indexed.

If `IndexAllAttributes` is set to `false` then only the attributes/tags listed in `IndexAttributes`
will be passed as annotations, other values will be passed as metadata.

#### `IndexActivityNames` (optional, default: `true`)

If `true` then `activity_display_name` and `activity_operation_name` corresponding to `Activity.DisplayName` 
and `Activity.OperationName` are passed as annotations.