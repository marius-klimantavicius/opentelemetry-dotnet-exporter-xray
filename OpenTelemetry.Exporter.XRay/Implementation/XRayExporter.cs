using System;
using System.Collections.Generic;
using System.Diagnostics;
using Amazon.XRay;
using Amazon.XRay.Model;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal class XRayExporter : BaseExporter<Activity>
    {
        private const int MaxDocumentSize = 62 * 1024;
        private const int MaxDocumentCount = 50;

        [ThreadStatic]
        private static List<string> _listCache;

        [ThreadStatic]
        private static PutTraceSegmentsRequest _requestCache;

        private readonly IAmazonXRay _client;
        private readonly XRayConverter _converter;

        public XRayExporter(XRayExporterOptions options)
        {
            _client = options.AmazonXRayClientFactory();

            _converter = new XRayConverter(
                options.ShouldIndexAttribute,
                options.IndexedAttributes,
                options.IndexAllAttributes,
                options.IndexActivityNames,
                options.ValidateTraceId,
                options.LogGroupNames);
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            using var scope = SuppressInstrumentationScope.Begin();

            try
            {
                var resource = ParentProvider.GetResource();
                var documentList = _listCache ?? new List<string>(MaxDocumentCount);
                var request = _requestCache ?? new PutTraceSegmentsRequest();

                var totalLength = 0;
                foreach (var item in batch)
                {
                    var document = _converter.Convert(resource, item);
                    if (document == null)
                        continue;

                    if (totalLength + document.Length > MaxDocumentSize || documentList.Count >= MaxDocumentCount)
                    {
                        request.TraceSegmentDocuments = documentList;
                        _client.PutTraceSegmentsAsync(request).GetAwaiter().GetResult();

                        totalLength = 0;
                        documentList.Clear();
                    }

                    totalLength += document.Length;
                    documentList.Add(document);
                }

                if (documentList.Count > 0)
                {
                    request.TraceSegmentDocuments = documentList;
                    _client.PutTraceSegmentsAsync(request).GetAwaiter().GetResult();

                    documentList.Clear();
                }

                request.TraceSegmentDocuments = null;

                _listCache = documentList;
                _requestCache = request;

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                XRayExporterEventSource.Log.FailedExport(ex);
                return ExportResult.Failure;
            }
        }
    }
}