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

#### `IndexAllAttributes` (optional, default: `true`)

If `IndexAllAttributes` is set to `true` all remaining scalar tags and attributes are passed as annotations
for AWS X-Ray service to be indexed.

If `IndexAllAttributes` is set to `false`, `IndexedAttributes`, `ShouldIndexAttribute`, and the span's
`aws.xray.annotations` string array select annotations. Other supported values are passed as metadata.
X-Ray annotations accept scalar values. Resource arrays and objects are stored as metadata;
set `IndexAllAttributes = false` and leave complex span attributes unindexed to retain them as metadata.

#### `IndexedAttributes`

A list of attributes to be passed as annotations to X-Ray (default: empty).
Resource attributes must have `"otel.resource."` prefix.
Explicitly listed attributes can be indexed even when also mapped to dedicated X-Ray fields,
such as `http.method`. A span can select additional attributes with an `aws.xray.annotations`
string array.

#### `ShouldIndexAttribute` (optional, default: `null`)

If provided then this function is used to determine which attributes/tags are to be passed via annotations
for X-Ray service to be indexed.

Explicit selections in `IndexedAttributes` and `aws.xray.annotations` take precedence over
`ShouldIndexAttribute`. `IndexAllAttributes = true` bypasses the callback.

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
            // index otel.resource.key (will be normalized to otel_resource_key) and
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
Linked trace IDs use the same validation setting. An invalid linked trace ID rejects the Activity
when validation is enabled.

#### `OverwriteInvalidTraceId` (optional, default: `false`)

When `ValidateTraceId` is enabled, replace an invalid Activity trace ID instead of rejecting the
Activity. Linked trace IDs are never rewritten.

#### `LogGroupNames` (optional, default: `null`)

List of log group names for cloud watch if not provided by resource attributes (`"aws.log.group.names"` and `"aws.log.group.arns"`).

#### `AllowDotInAnnotationKey` (optional, default: `false`)

If `true` then dots in attribute keys are allowed and will not be replaced with underscores.
Corresponds to BETA feature flag "exporter.xray.allowDot".
Original doc: `X-Ray Exporter will no longer convert . to _ in annotation keys when this feature gate is enabled.`

#### Database and HTTP/network conventions

Set `EmitV1DBConventions = true` to map `db.namespace`, `db.system.name`, `db.query.text`, and
`user.name` to X-Ray SQL fields. Nonempty modern values take precedence over legacy aliases;
legacy attributes remain supported as fallbacks.

Set `EmitV1HTTPNetworkConventions = true` to prefer modern HTTP/network aliases, use
`server.address`/`server.port` for client URLs, and support `network.peer.port` and
`messaging.message.body.size`. Existing stable HTTP aliases remain available with the default settings.

To ignore legacy mappings, also set `DontEmitV0DBConventions = true` or
`DontEmitV0HTTPNetworkConventions = true`. Each requires its corresponding `EmitV1` option;
invalid combinations throw `ArgumentException` when the exporter is configured.

### X-Ray attributes

Activity links are exported with their trace ID, span ID, and attributes. Consumer Activities remain
segments even when they have a parent; producer Activities use the remote namespace unless an AWS
namespace applies.

With `IndexAllAttributes = false`, unindexed JSON objects in string attributes named
`aws.xray.metadata.<namespace>` are exported into that metadata namespace.
The `default` namespace merges with ordinary metadata. Invalid JSON or a
non-object value is retained under its original attribute name in default metadata.

Application Signals attributes such as `aws.local.service`, `aws.remote.service`, and
`aws.span.kind` are supported. A client, producer, or consumer marked `aws.span.kind = LOCAL_ROOT`
exports both a service segment and a dependency subsegment. The service segment uses
`aws.local.service`, falling back to resource `service.name`.

A boolean `aws.xray.inprogress = true` exports `in_progress` without `end_time`. A false marker
exports the normal end time. This attribute controls translation of an exported Activity; the
standard export processors export Activities when they end.
