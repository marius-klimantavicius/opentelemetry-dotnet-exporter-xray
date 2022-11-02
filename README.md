# AWS X-Ray Exporter for OpenTelemetry .NET

**NOTE: This exporter is not affiliated with or officially supported by Amazon.**

This is a port of [AWS X-Ray Tracing Exporter for OpenTelemetry Collector](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/exporter/awsxrayexporter).

[![NuGet](https://img.shields.io/nuget/v/Marius.OpenTelemetry.Exporter.XRay.svg)](https://www.nuget.org/packages/Marius.OpenTelemetry.Exporter.XRay)
[![NuGet](https://img.shields.io/nuget/dt/Marius.OpenTelemetry.Exporter.XRay.svg)](https://www.nuget.org/packages/Marius.OpenTelemetry.Exporter.XRay)

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

#### `IndexedAttributes`

A list of attributes to be passed as annotations to X-Ray.
Resource attributes must have `"otel.resource."` prefix.

#### `ShouldIndexAttribute` (optional, default: `null`)

If provided then this function is used to determine which attributes/tags are to be passed via annotations
for X-Ray service to be indexed.

`ShouldIndexAttribute` is only called for attributes that are not in `IndexedAttributes` list.

If function returns `true` then attribute/tag is passed as annotation.

If function returns `false` then attribute/tag is passed as metadata.

Example:

```csharp

options.IndexAllAttributes = false; // Set to false otherwise ShouldIndexAttribute is ignored
options.ShouldIndexAttribute = ShouldIndexAttribute;

bool ShouldIndexAttribute(string name, bool isResource)
{
    if (isResource)
    {
        // In case of resource the final key is "otel.resource." + name
        // var key = "otel.resource." + name;
        if (name == "build"
            || name.Contains("internal"))
        {
            // index otel.resource.key (will ne normalized to otel_resource_key) and 
            // any other key that contains "internal"
            return true;
        }
        
        return false;
    }
    
    // Only index tenant_id and correlation_id
    return name == "tenant_id" || name == "correlation_id";
}

```

Both indexed attributes and function:
```csharp
options.IndexAllAttributes = false; // Set to false otherwise ShouldIndexAttribute is ignored
options.ShouldIndexAttribute = ShouldIndexAttribute;
options.IndexedAttributes = new[] 
{
    "otel.resource.build",
    "tenant_id",
    "correlation_id",
};

bool ShouldIndexAttribute(string name, bool isResource)
{
    if (isResource)
    {
        // In case of resource the final key is "otel.resource." + name
        // var key = "otel.resource." + name;
        return name.Contains("internal");
    }
    
    // "tenant_id" and "correlation_id" are already indexed
    // don't index anything else
    return false;
}

```

#### `IndexActivityNames` (optional, default: `true`)

If `true` then `activity_display_name` and `activity_operation_name` corresponding to `Activity.DisplayName`
and `Activity.OperationName` are passed as annotations.

#### `ValidateTraceId` (optional, default: `false`)

If `true` then will reject/ignore activities that have either invalid aws trace id or trace id has expired.
