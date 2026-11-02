using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
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
            var hasNetPeerAddress = false;
            var urlParts = new XRayHttpUrlParts();

            var spanTags = context.SpanTags;
            // Stable aliases remain available by default. Opting in makes nonempty modern
            // values win, while reading both enabled keys consumes both aliases.
            if ((!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpMethod(out var value)) || spanTags.TryGetAttributeHttpRequestMethod(out value))
            {
                requestMethod = value.AsString();
                hasHttp = true;
                if (spanTags.TryGetAttributeHttpRequestMethod(out value) && _emitV1HttpNetworkConventions && !string.IsNullOrEmpty(value.AsString()))
                    requestMethod = value.AsString();
            }

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpClientIp(out value))
            {
                requestClientIp = value.AsString();
                hasHttp = true;
            }

            if ((!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpUserAgent(out value)) || spanTags.TryGetAttributeUserAgentOriginal(out value))
            {
                requestUserAgent = value.AsString();
                hasHttp = true;
                if (spanTags.TryGetAttributeUserAgentOriginal(out value) && _emitV1HttpNetworkConventions && !string.IsNullOrEmpty(value.AsString()))
                    requestUserAgent = value.AsString();
            }

            if ((!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpStatusCode(out value)) || spanTags.TryGetAttributeHttpResponseStatusCode(out value))
            {
                responseStatus = value.AsInt();
                hasHttp = true;
                if (spanTags.TryGetAttributeHttpResponseStatusCode(out value) && _emitV1HttpNetworkConventions && IsHttpInteger(value))
                    responseStatus = value.AsInt();
            }

            if ((!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpUrl(out value)) || spanTags.TryGetAttributeUrlFull(out value))
            {
                urlParts.AttributeHttpUrl = value.AsString();
                hasHttp = true;
                hasHttpRequestUrlAttributes = true;
                if (spanTags.TryGetAttributeUrlFull(out value) && _emitV1HttpNetworkConventions && !string.IsNullOrEmpty(value.AsString()))
                    urlParts.AttributeHttpUrl = value.AsString();
            }

            if ((!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpScheme(out value)) || spanTags.TryGetAttributeUrlScheme(out value))
            {
                urlParts.AttributeHttpScheme = value.AsString();
                hasHttp = true;
                if (spanTags.TryGetAttributeUrlScheme(out value) && _emitV1HttpNetworkConventions && !string.IsNullOrEmpty(value.AsString()))
                    urlParts.AttributeHttpScheme = value.AsString();
            }

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpHost(out value))
            {
                urlParts.AttributeHttpHost = value.AsString();
                hasHttp = true;
                hasHttpRequestUrlAttributes = true;
            }

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpTarget(out value))
            {
                urlParts.AttributeHttpTarget = value.AsString();
                hasHttp = true;
            }

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeHttpServerName(out value))
            {
                urlParts.AttributeHttpServerName = value.AsString();
                hasHttp = true;
                hasHttpRequestUrlAttributes = true;
            }

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeNetHostPort(out value))
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

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeNetHostName(out value))
            {
                urlParts.AttributeNetHostName = value.AsString();
                hasHttpRequestUrlAttributes = true;
            }

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeNetPeerName(out value))
            {
                urlParts.AttributeNetPeerName = value.AsString();
                hasHttpRequestUrlAttributes = true;
            }

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeNetPeerPort(out value))
            {
                urlParts.AttributeNetPeerPort = value.AsString();
                if (string.IsNullOrEmpty(urlParts.AttributeNetPeerPort))
                    urlParts.AttributeNetPeerPort = value.AsInt().ToString(CultureInfo.InvariantCulture);
            }

            if (!_dontEmitV0HttpNetworkConventions && spanTags.TryGetAttributeNetPeerIp(out value))
            {
                // Prefer HTTP forwarded information (AttributeHTTPClientIP) when present.
                requestClientIp ??= value.AsString();
                urlParts.AttributeNetPeerIp = value.AsString();
                hasHttpRequestUrlAttributes = true;
                hasNetPeerAddress = true;
            }

            if (spanTags.TryGetAttributeNetworkPeerAddress(out value))
            {
                if (IPAddress.TryParse(value.AsString(), out _))
                {
                    requestClientIp ??= value.AsString();
                    hasHttpRequestUrlAttributes = true;
                    hasNetPeerAddress = true;
                }
            }

            if (spanTags.TryGetAttributeClientAddress(out value))
            {
                if (IPAddress.TryParse(value.AsString(), out _))
                    requestClientIp = value.AsString();
            }

            if (_emitV1HttpNetworkConventions && spanTags.TryGetAttributeNetworkPeerPort(out value) && TryGetHttpPort(value, out var networkPeerPort))
                urlParts.AttributeNetworkPeerPort = networkPeerPort;

            if (spanTags.TryGetAttributeUrlPath(out value))
            {
                urlParts.AttributeUrlPath = value.AsString();
                hasHttp = true;
            }

            if (spanTags.TryGetAttributeUrlQuery(out value))
            {
                urlParts.AttributeUrlQuery = value.AsString();
                hasHttp = true;
            }

            if (spanTags.TryGetAttributeServerAddress(out value))
            {
                if (!_emitV1HttpNetworkConventions || !string.IsNullOrEmpty(value.AsString()))
                {
                    urlParts.AttributeServerAddress = value.AsString();
                    hasHttpRequestUrlAttributes = true;
                }
            }

            if (spanTags.TryGetAttributeServerPort(out value))
            {
                if (TryGetHttpPort(value, out var serverPort))
                    urlParts.AttributeServerPort = serverPort;
                else if (!_emitV1HttpNetworkConventions)
                    urlParts.AttributeServerPort = value.AsInt().ToString(CultureInfo.InvariantCulture);
            }

            if (!hasNetPeerAddress && requestClientIp != null)
                requestXForwardedFor = true;

            if (!hasHttp)
            {
                spanTags.Consume();
                return;
            }

            if (spanTags.TryGetAttributeHttpResponseContentLength(out value) && value != null)
                responseContentLength = value.AsInt();
            else
                responseContentLength = ExtractResponseSizeFromEvents(context);

            if (context.IsServiceSegment)
            {
                spanTags.Consume();
                return;
            }

            var writer = context.Writer;
            writer.WritePropertyName(XRayField.Http);
            writer.WriteStartObject();

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

        private static bool IsHttpInteger(object value)
        {
            return value is int || value is uint || value is long;
        }

        private static bool TryGetHttpPort(object value, out string port)
        {
            port = value.AsString();
            if (!string.IsNullOrEmpty(port))
                return true;

            if (!IsHttpInteger(value))
                return false;

            port = value.AsInt().ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private long? ExtractResponseSizeFromEvents(in XRayConverterContext context)
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

        private long? ExtractResponseSizeFromAttributes(XRayTagDictionary tagDictionary)
        {
            if (tagDictionary.TryGetAttributeMessageType(out var value) && value.AsString() == "RECEIVED")
            {
                var size = default(long?);
                if (!_dontEmitV0HttpNetworkConventions && tagDictionary.TryGetAttributeMessagingPayloadSize(out value))
                    size = value.AsInt();

                // Match the alias precedence used for HTTP attributes, including a valid zero size.
                if (_emitV1HttpNetworkConventions && tagDictionary.TryGetAttributeMessagingMessageBodySize(out value) && IsHttpInteger(value))
                    size = value.AsInt();

                return size;
            }

            return null;
        }

        private long? ExtractResponseSizeFromAttributes(IEnumerable<KeyValuePair<string, object>> tags)
        {
            var isReceived = false;
            var result = default(long?);
            var modernResult = default(long?);
            foreach (var (key, value) in tags)
            {
                if (key == XRayConventions.AttributeMessageType && value.AsString() == "RECEIVED")
                    isReceived = true;
                else if (!_dontEmitV0HttpNetworkConventions && key == XRayConventions.AttributeMessagingPayloadSize)
                    result = value.AsInt();
                else if (_emitV1HttpNetworkConventions && key == XRayConventions.AttributeMessagingMessageBodySize && IsHttpInteger(value))
                    modernResult = value.AsInt();
            }

            if (isReceived)
                return modernResult ?? result;

            return null;
        }

        private void WriteClientUrl(Utf8JsonWriter writer, ref XRayHttpUrlParts urlParts)
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
            var hasModernHost = urlParts.AttributeServerAddress.TryGetValue(out var modernHost);
            if (!urlParts.AttributeHttpHost.TryGetValue(out var host) || (_emitV1HttpNetworkConventions && hasModernHost))
            {
                if (_emitV1HttpNetworkConventions && hasModernHost)
                    host = modernHost;
                else if (!urlParts.AttributeNetPeerName.TryGetValue(out host) && !urlParts.AttributeNetPeerIp.TryGetValue(out host))
                    host = modernHost;

                if (!_emitV1HttpNetworkConventions || !urlParts.AttributeServerPort.TryGetValue(out port))
                {
                    if (!urlParts.AttributeNetPeerPort.TryGetValue(out port))
                    {
                        if (!urlParts.AttributeNetworkPeerPort.TryGetValue(out port))
                            port = "";
                    }
                }
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

            AppendHttpTarget(ref sb, ref urlParts);

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
                    {
                        if (!urlParts.AttributeHostName.TryGetValue(out host))
                            urlParts.AttributeServerAddress.TryGetValue(out host);
                    }
                }

                if (!urlParts.AttributeNetHostPort.TryGetValue(out port))
                {
                    if (!urlParts.AttributeServerPort.TryGetValue(out port))
                        port = "";
                }
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

            AppendHttpTarget(ref sb, ref urlParts);

            writer.WriteString(XRayField.Url, sb.AsSpan());
            sb.Dispose();
        }

        private static void AppendHttpTarget(ref ValueStringBuilder sb, ref XRayHttpUrlParts urlParts)
        {
            if (urlParts.AttributeHttpTarget.TryGetValue(out var target))
            {
                sb.Append(target);
            }
            else
            {
                if (urlParts.AttributeUrlPath.TryGetValue(out var path))
                    sb.Append(path);
                else
                    sb.Append('/');

                if (urlParts.AttributeUrlQuery.TryGetValue(out var query))
                {
                    if (!query.StartsWith("?", System.StringComparison.Ordinal))
                        sb.Append('?');
                    sb.Append(query);
                }
            }
        }
    }
}