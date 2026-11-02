using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            _converter = new XRayConverter(
                options.ShouldIndexAttribute,
                options.IndexedAttributes,
                options.IndexAllAttributes,
                options.IndexActivityNames,
                options.ValidateTraceId,
                options.OverwriteInvalidTraceId,
                options.LogGroupNames,
                options.AllowDotInAnnotationKey,
                options.EmitV1DbConventions,
                options.DontEmitV0DbConventions,
                options.EmitV1HttpNetworkConventions,
                options.DontEmitV0HttpNetworkConventions);

            _client = options.AmazonXRayClientFactory();
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            using var scope = SuppressInstrumentationScope.Begin();

            var documentList = _listCache ?? new List<string>(MaxDocumentCount);
            var request = _requestCache ?? new PutTraceSegmentsRequest();
            
            // Take ownership to avoid sharing in case of recursive calls
            _listCache = null;
            _requestCache = null;

            try
            {
                var resource = ParentProvider.GetResource();

                var totalLength = 0;
                foreach (var item in batch)
                {
                    var (first, second) = _converter.ConvertDocuments(resource, item);
                    if (first == null)
                        continue;

                    if (totalLength + first.Length > MaxDocumentSize || documentList.Count >= MaxDocumentCount)
                    {
                        request.TraceSegmentDocuments = documentList;
                        var response = _client.PutTraceSegmentsAsync(request).GetAwaiter().GetResult();
                        if (response?.UnprocessedTraceSegments?.Count > 0)
                            XRayExporterEventSource.Log.UnprocessedTraceSegments(response.UnprocessedTraceSegments);
                    
                        totalLength = 0;
                        documentList.Clear();
                    }
                    
                    totalLength += first.Length;
                    documentList.Add(first);
                    
                    if (second == null)
                        continue;

                    if (totalLength + second.Length > MaxDocumentSize || documentList.Count >= MaxDocumentCount)
                    {
                        request.TraceSegmentDocuments = documentList;
                        var response = _client.PutTraceSegmentsAsync(request).GetAwaiter().GetResult();
                        if (response?.UnprocessedTraceSegments?.Count > 0)
                            XRayExporterEventSource.Log.UnprocessedTraceSegments(response.UnprocessedTraceSegments);
                    
                        totalLength = 0;
                        documentList.Clear();
                    }
                    
                    totalLength += second.Length;
                    documentList.Add(second);
                }

                if (documentList.Count > 0)
                {
                    request.TraceSegmentDocuments = documentList;
                    var response = _client.PutTraceSegmentsAsync(request).GetAwaiter().GetResult();
                    if (response?.UnprocessedTraceSegments?.Count > 0)
                        XRayExporterEventSource.Log.UnprocessedTraceSegments(response.UnprocessedTraceSegments);

                    documentList.Clear();
                }

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                XRayExporterEventSource.Log.FailedExport(ex);
                return ExportResult.Failure;
            }
            finally
            {
                documentList.Clear();
                request.TraceSegmentDocuments = null;

                _listCache = documentList;
                _requestCache = request;
            }
        }
    }
}