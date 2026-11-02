using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.XRay;
using Amazon.XRay.Model;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public class XRayExporterCoreTests : XRayTest
    {
        private class RecordingClient : AmazonXRayClient
        {
            public readonly List<string[]> Batches = new List<string[]>();
            public bool FailNext;

            public RecordingClient() : base(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1)
            {
            }

            public override Task<PutTraceSegmentsResponse> PutTraceSegmentsAsync(PutTraceSegmentsRequest request, CancellationToken cancellationToken = default)
            {
                if (FailNext)
                {
                    FailNext = false;
                    throw new InvalidOperationException("test transport failure");
                }

                Batches.Add(request.TraceSegmentDocuments.ToArray());
                return Task.FromResult(new PutTraceSegmentsResponse());
            }
        }

        [Fact]
        public void Exporter_emits_every_generated_document_and_splits_on_document_count()
        {
            using var client = new RecordingClient();
            using var exporter = new XRayExporter(new XRayExporterOptions { AmazonXRayClientFactory = () => client, IndexActivityNames = false });
            using var provider = Sdk.CreateTracerProviderBuilder().AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();
            
            var spans = Enumerable.Range(0, 26).Select(_ => ConstructSpan(ActivityKind.Client, new Dictionary<string, object> { ["aws.span.kind"] = "LOCAL_ROOT", ["aws.local.service"] = "local", ["aws.remote.service"] = "remote" })).ToArray();
            try
            {
                Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>(spans, spans.Length)));
                Assert.Equal(new[] { 50, 2 }, client.Batches.Select(batch => batch.Length).ToArray());
                
                var documents = client.Batches.SelectMany(batch => batch).Select(document => JsonSerializer.Deserialize<Model.XRaySegment>(document)).ToArray();
                for (var i = 0; i < spans.Length; i++)
                {
                    var dependency = documents[i * 2];
                    var service = documents[i * 2 + 1];
                    Assert.Equal("subsegment", dependency.Type);
                    Assert.Equal(spans[i].SpanId.ToHexString(), dependency.Id);
                    Assert.Equal(service.Id, dependency.ParentId);
                    Assert.Equal("local", service.Name);
                    Assert.Null(service.Type);
                }
            }
            finally
            {
                foreach (var span in spans)
                    span.Dispose();
            }
        }

        [Fact]
        public void Exporter_skips_rejected_spans_and_exports_single_and_paired_documents_in_order()
        {
            using var client = new RecordingClient();
            using var exporter = new XRayExporter(new XRayExporterOptions
            {
                AmazonXRayClientFactory = () => client,
                ValidateTraceId = true,
            });
            using var provider = Sdk.CreateTracerProviderBuilder().AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();
            var invalidContext = new ActivityContext(ActivityTraceId.CreateFromString("00000001aaaaaaaaaaaaaaaaaaaaaaaa".AsSpan()), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using var rejected = ConstructSpan(ActivityKind.Consumer, links: new[] { new ActivityLink(invalidContext) });
            using var single = ConstructSpan(ActivityKind.Server);
            using var paired = ConstructSpan(ActivityKind.Client,
                new Dictionary<string, object> { ["aws.span.kind"] = "LOCAL_ROOT", ["aws.local.service"] = "local" });

            Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>(new[] { rejected, single, paired }, 3)));

            var documents = Assert.Single(client.Batches);
            Assert.Equal(3, documents.Length);

            var segments = documents.Select(document => JsonSerializer.Deserialize<Model.XRaySegment>(document)).ToArray();
            Assert.Equal(single.SpanId.ToHexString(), segments[0].Id);
            Assert.Equal(paired.SpanId.ToHexString(), segments[1].Id);
            Assert.Equal(segments[2].Id, segments[1].ParentId);
            Assert.Equal("local", segments[2].Name);
        }

        [Fact]
        public void Exporter_passes_convention_options_to_converter()
        {
            using var client = new RecordingClient();
            using var exporter = new XRayExporter(new XRayExporterOptions
            {
                AmazonXRayClientFactory = () => client,
                EmitV1DbConventions = true, DontEmitV0DbConventions = true,
                EmitV1HttpNetworkConventions = true, DontEmitV0HttpNetworkConventions = true,
            });
            using var provider = Sdk.CreateTracerProviderBuilder().AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();
            using var span = ConstructSpan(ActivityKind.Client, new Dictionary<string, object>
            {
                ["db.namespace"] = "modern", ["db.system.name"] = "postgresql", ["db.query.text"] = "SELECT 1",
                ["server.address"] = "modern.example.com", ["http.host"] = "legacy", ["db.name"] = "legacy",
                ["http.request.method"] = "GET",
            });
            Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>(new[] { span }, 1)));
            
            var document = Assert.Single(Assert.Single(client.Batches));
            using var parsed = JsonDocument.Parse(document);
            Assert.Equal("modern@modern.example.com", parsed.RootElement.GetProperty("name").GetString());
            Assert.Equal("SELECT 1", parsed.RootElement.GetProperty("sql").GetProperty("sanitized_query").GetString());
            Assert.Contains("modern.example.com", parsed.RootElement.GetProperty("http").GetProperty("request").GetProperty("url").GetString()!);
        }

        [Fact]
        public void Failed_export_does_not_retain_documents_in_thread_cache()
        {
            using var client = new RecordingClient();
            using var exporter = new XRayExporter(new XRayExporterOptions { AmazonXRayClientFactory = () => client });
            using var provider = Sdk.CreateTracerProviderBuilder().AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();
            using var span = ConstructSpan(ActivityKind.Server);
            
            var batch = new Batch<Activity>(new[] { span }, 1);
            Assert.Equal(ExportResult.Success, exporter.Export(batch));
            client.FailNext = true;
            Assert.Equal(ExportResult.Failure, exporter.Export(batch));
            Assert.Equal(ExportResult.Success, exporter.Export(batch));
            Assert.Equal(2, client.Batches.Count);
            Assert.All(client.Batches, documents => Assert.Single(documents));
        }
    }
}