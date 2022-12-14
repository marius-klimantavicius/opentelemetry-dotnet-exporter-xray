using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private void WriteHttp(in XRayConverterContext context)
        {
            var requestXForwardedFor = default(bool?);
            var requestMethod = default(string);
            var requestUserAgent = default(string);
            var requestClientIp = default(string);
            var responseStatus = default(long?);
            var responseContentLength = default(long?);

            var hasHttp = false;
            var hasHttpRequestUrlAttributes = false;
            var urlParts = new XRayHttpUrlParts();

            var spanTags = context.SpanTags;
            if (spanTags.TryGetAttributeHttpMethod(out var value))
            {
                requestMethod = value.AsString();
                hasHttp = true;
            }

            if (spanTags.TryGetAttributeHttpClientIp(out value))
            {
                requestClientIp = value.AsString();
                requestXForwardedFor = true;
                hasHttp = true;
            }

            if (spanTags.TryGetAttributeHttpUserAgent(out value))
            {
                requestUserAgent = value.AsString();
                hasHttp = true;
            }

            if (spanTags.TryGetAttributeHttpStatusCode(out value))
            {
                responseStatus = value.AsInt();
                hasHttp = true;
            }

            if (spanTags.TryGetAttributeHttpUrl(out value))
            {
                urlParts.AttributeHttpUrl = value.AsString();
                hasHttp = true;
                hasHttpRequestUrlAttributes = true;
            }

            if (spanTags.TryGetAttributeHttpScheme(out value))
            {
                urlParts.AttributeHttpScheme = value.AsString();
                hasHttp = true;
            }

            if (spanTags.TryGetAttributeHttpHost(out value))
            {
                urlParts.AttributeHttpHost = value.AsString();
                hasHttp = true;
                hasHttpRequestUrlAttributes = true;
            }

            if (spanTags.TryGetAttributeHttpTarget(out value))
            {
                urlParts.AttributeHttpTarget = value.AsString();
                hasHttp = true;
            }

            if (spanTags.TryGetAttributeHttpServerName(out value))
            {
                urlParts.AttributeHttpServerName = value.AsString();
                hasHttp = true;
                hasHttpRequestUrlAttributes = true;
            }

            if (spanTags.TryGetAttributeNetHostPort(out value))
            {
                urlParts.AttributeNetHostPort = value.AsString();
                hasHttp = true;
                if (string.IsNullOrEmpty(urlParts.AttributeNetHostPort))
                    urlParts.AttributeNetHostPort = value.AsInt().ToString(CultureInfo.InvariantCulture);
            }

            if (spanTags.TryGetAttributeHostName(out value))
            {
                urlParts.AttributeHostName = value.AsString();
                hasHttpRequestUrlAttributes = true;
            }

            if (spanTags.TryGetAttributeNetHostName(out value))
            {
                urlParts.AttributeNetHostName = value.AsString();
                hasHttpRequestUrlAttributes = true;
            }

            if (spanTags.TryGetAttributeNetPeerName(out value))
            {
                urlParts.AttributeNetPeerName = value.AsString();
            }

            if (spanTags.TryGetAttributeNetPeerPort(out value))
            {
                urlParts.AttributeNetPeerPort = value.AsString();
                if (string.IsNullOrEmpty(urlParts.AttributeNetPeerPort))
                    urlParts.AttributeNetPeerPort = value.AsInt().ToString(CultureInfo.InvariantCulture);
            }

            if (spanTags.TryGetAttributeNetPeerIp(out value))
            {
                // Prefer HTTP forwarded information (AttributeHTTPClientIP) when present.
                requestClientIp ??= value.AsString();
                urlParts.AttributeNetPeerIp = value.AsString();
                hasHttpRequestUrlAttributes = true;
            }

            if (!hasHttp)
            {
                spanTags.Consume();
                return;
            }

            var writer = context.Writer;
            writer.WritePropertyName(XRayField.Http);
            writer.WriteStartObject();

            if (spanTags.TryGetAttributeHttpResponseContentLength(out value) && value != null)
                responseContentLength = value.AsInt();
            else
                responseContentLength = ExtractResponseSizeFromEvents(context);

            writer.WritePropertyName(XRayField.Request);
            writer.WriteStartObject();

            if (requestXForwardedFor != null)
                writer.WriteBoolean(XRayField.XForwardedFor, requestXForwardedFor.GetValueOrDefault());
            if (requestMethod != null)
                writer.WriteString(XRayField.Method, requestMethod);

            if (hasHttpRequestUrlAttributes)
            {
                if (context.Span.Kind == ActivityKind.Server)
                    WriteServerUrl(writer, ref urlParts);
                else
                    WriteClientUrl(writer, ref urlParts);
            }

            if (requestUserAgent != null)
                writer.WriteString(XRayField.UserAgent, requestUserAgent);
            if (requestClientIp != null)
                writer.WriteString(XRayField.ClientIp, requestClientIp);

            writer.WriteEndObject();

            writer.WritePropertyName(XRayField.Response);
            writer.WriteStartObject();

            if (responseStatus != null)
                writer.WriteNumber(XRayField.Status, responseStatus.GetValueOrDefault());

            if (responseContentLength != null)
                writer.WriteNumber(XRayField.ContentLength, responseContentLength.GetValueOrDefault());

            writer.WriteEndObject();

            spanTags.Consume();
            writer.WriteEndObject();
        }

        private static long? ExtractResponseSizeFromEvents(in XRayConverterContext context)
        {
            var size = ExtractResponseSizeFromAttributes(context.SpanTags);
            if (size != null)
                return size;

            foreach (var ev in context.Span.Events)
            {
                size = ExtractResponseSizeFromAttributes(ev.Tags);
                if (size != null)
                    return size;
            }

            return null;
        }

        private static long? ExtractResponseSizeFromAttributes(XRayTagDictionary tagDictionary)
        {
            if (tagDictionary.TryGetAttributeMessageType(out var value) && value.AsString() == "RECEIVED")
            {
                if (tagDictionary.TryGetAttributeMessagingPayloadSize(out value))
                    return value.AsInt();
            }

            return null;
        }

        private static long? ExtractResponseSizeFromAttributes(IEnumerable<KeyValuePair<string, object>> tags)
        {
            var isReceived = false;
            var result = default(long?);
            foreach (var (key, value) in tags)
            {
                if (key == XRayConventions.AttributeMessageType && value.AsString() == "RECEIVED")
                {
                    isReceived = true;
                }
                else if (key == XRayConventions.AttributeMessagingPayloadSize)
                {
                    result = value.AsInt();
                }
            }

            if (isReceived)
                return result;

            return null;
        }

        private static void WriteClientUrl(Utf8JsonWriter writer, ref XRayHttpUrlParts urlParts)
        {
            if (urlParts.AttributeHttpUrl.TryGetValue(out var url))
            {
                if (!string.IsNullOrEmpty(url))
                    writer.WriteString(XRayField.Url, url);

                return;
            }

            if (!urlParts.AttributeHttpScheme.TryGetValue(out var scheme))
                scheme = "http";

            var port = "";
            if (!urlParts.AttributeHttpHost.TryGetValue(out var host))
            {
                if (!urlParts.AttributeNetPeerName.TryGetValue(out host))
                    urlParts.AttributeNetPeerIp.TryGetValue(out host);

                if (!urlParts.AttributeNetPeerPort.TryGetValue(out port))
                    port = "";
            }

            var sb = new ValueStringBuilder(stackalloc char[128]);
            sb.Append(scheme);
            sb.Append("://");
            sb.Append(host);
            if (!string.IsNullOrEmpty(port) && !(scheme == "http" && port == "80") && !(scheme == "https" && port == "443"))
            {
                sb.Append(":");
                sb.Append(port);
            }

            if (urlParts.AttributeHttpTarget.TryGetValue(out var target))
                sb.Append(target);
            else
                sb.Append('/');

            writer.WriteString(XRayField.Url, sb.AsSpan());
            sb.Dispose();
        }

        private static void WriteServerUrl(Utf8JsonWriter writer, ref XRayHttpUrlParts urlParts)
        {
            if (urlParts.AttributeHttpUrl.TryGetValue(out var url))
            {
                if (!string.IsNullOrEmpty(url))
                    writer.WriteString(XRayField.Url, url);

                return;
            }

            if (!urlParts.AttributeHttpScheme.TryGetValue(out var scheme))
                scheme = "http";

            var port = "";
            if (!urlParts.AttributeHttpHost.TryGetValue(out var host))
            {
                if (!urlParts.AttributeHttpServerName.TryGetValue(out host))
                {
                    if (!urlParts.AttributeNetHostName.TryGetValue(out host))
                        urlParts.AttributeHostName.TryGetValue(out host);
                }

                if (!urlParts.AttributeNetHostPort.TryGetValue(out port))
                    port = "";
            }

            var sb = new ValueStringBuilder(stackalloc char[128]);
            sb.Append(scheme);
            sb.Append("://");
            sb.Append(host);
            if (!string.IsNullOrEmpty(port) && !(scheme == "http" && port == "80") && !(scheme == "https" && port == "443"))
            {
                sb.Append(":");
                sb.Append(port);
            }

            if (urlParts.AttributeHttpTarget.TryGetValue(out var target))
                sb.Append(target);
            else
                sb.Append('/');

            writer.WriteString(XRayField.Url, sb.AsSpan());
            sb.Dispose();
        }
    }
}