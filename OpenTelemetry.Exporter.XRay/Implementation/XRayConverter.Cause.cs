using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private void WriteCause(in XRayConverterContext context)
        {
            var span = context.Span;
            var status = span.Status;

            var message = default(string);
            var hasExceptions = false;

            foreach (var ev in span.Events)
            {
                if (ev.Name == XRayConventions.ExceptionEventName)
                    hasExceptions = true;
            }

            if (status == ActivityStatusCode.Unset)
            {
                if (context.SpanTags.TryGetStatusCodeKey(out var value))
                {
                    if (value.AsString() == "ERROR")
                        status = ActivityStatusCode.Error;
                }

                context.SpanTags.ResetConsume();
            }

            var writer = context.Writer;
            if (hasExceptions)
            {
                var resourceAttributes = context.ResourceAttributes;
                var language = "dotnet";
                if (resourceAttributes.TryGetAttributeTelemetrySdkLanguage(out var value))
                    language = value.AsString();

                writer.WritePropertyName(XRayWriter.Cause);
                writer.WriteStartObject();

                writer.WritePropertyName(XRayWriter.Exceptions);
                writer.WriteStartArray();

                foreach (var ev in span.Events)
                {
                    if (ev.Name == XRayConventions.ExceptionEventName)
                    {
                        var exceptionType = "";
                        var stackTrace = "";

                        message = "";

                        if (ev.Tags.TryGetValue(XRayConventions.AttributeExceptionType, out value))
                            exceptionType = value.AsString();

                        if (ev.Tags.TryGetValue(XRayConventions.AttributeExceptionMessage, out value))
                            message = value.AsString();

                        if (ev.Tags.TryGetValue(XRayConventions.AttributeExceptionStacktrace, out value))
                            stackTrace = value.AsString();

                        WriteException(writer, exceptionType, message, stackTrace, language);
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            else if (status == ActivityStatusCode.Error)
            {
                message = span.StatusDescription;

                var spanTags = context.SpanTags;
                if (spanTags.TryGetAttributeHttpStatusText(out var value))
                {
                    if (string.IsNullOrEmpty(message))
                        message = value.AsString();
                }

                spanTags.Consume();

                if (!string.IsNullOrEmpty(message))
                {
                    var id = NewSegmentId();
                    var hexId = id.ToHexString();

                    writer.WritePropertyName(XRayWriter.Cause);
                    writer.WriteStartObject();

                    writer.WritePropertyName(XRayWriter.Exceptions);
                    writer.WriteStartArray();

                    writer.WriteStartObject();

                    writer.WriteString(XRayWriter.Id, hexId);
                    writer.WriteString(XRayWriter.Message, message);

                    writer.WriteEndObject();

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
            }

            var isError = false;
            var isThrottle = false;
            var isFault = false;
            if (status == ActivityStatusCode.Error)
            {
                // we can't use tag dictionary here because http status code might have been already consumed
                if (context.Span.TagObjects.TryGetValue(XRayConventions.AttributeHttpStatusCode, out var value))
                {
                    var code = value.AsInt();
                    if (code >= 400 && code < 499)
                    {
                        isError = true;
                        if (code == 429)
                            isThrottle = true;
                    }
                    else
                    {
                        isFault = true;
                    }
                }
                else
                {
                    isFault = true;
                }

                context.SpanTags.ResetConsume();
            }

            writer.WriteBoolean(XRayWriter.Error, isError);
            writer.WriteBoolean(XRayWriter.Throttle, isThrottle);
            writer.WriteBoolean(XRayWriter.Fault, isFault);
        }

        private void WriteException(Utf8JsonWriter writer, string exceptionType, string message, string stacktrace, string language)
        {
            writer.WriteStartObject();
            writer.WriteString(XRayWriter.Id, NewSegmentId().ToHexString());
            if (exceptionType != null)
                writer.WriteString(XRayWriter.Type, exceptionType);
            if (message != null)
                writer.WriteString(XRayWriter.Message, message);

            if (!string.IsNullOrEmpty(stacktrace))
            {
                // Right now we only care about dotnet, other languages can be ported on-demand
                switch (language)
                {
                    // case "java":
                    // case "python":
                    // case "javascript":
                    case "dotnet":
                        FillDotNetStacktrace(writer, stacktrace);
                        break;
                    // case "php":
                    // case "go"
                }
            }

            writer.WriteEndObject();
        }

        private void FillDotNetStacktrace(Utf8JsonWriter writer, string stacktrace)
        {
            var r = new StringReader(stacktrace);

            // Skip first line containing top level message
            if (r.ReadLine() == null)
                return;

            var line = r.ReadLine();
            if (line == null)
                return;

            var hasStack = false;
            while (line != null)
            {
                var prefix = -1;
                if (line.StartsWith("\tat"))
                    prefix = 4;
                else if (line.StartsWith("   at"))
                    prefix = 6;
                if (prefix >= 0)
                {
                    var index = line.IndexOf(" in ", StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        var parts = line.Split(" in ");
                        var label = parts[0][prefix..];
                        var path = parts[1];
                        var lineNumber = 0;

                        var colonIndex = parts[1].LastIndexOf(':');
                        if (colonIndex >= 0)
                        {
                            var lineString = path[(colonIndex + 1)..];
                            if (lineString.StartsWith("line"))
                                lineString = lineString[5..];

                            path = path[..colonIndex];
                            int.TryParse(lineString, NumberStyles.Integer, CultureInfo.InvariantCulture, out lineNumber);
                        }

                        if (!hasStack)
                        {
                            writer.WritePropertyName(XRayWriter.Stack);
                            writer.WriteStartArray();
                            hasStack = true;
                        }

                        writer.WriteStartObject();
                        if (path != null)
                            writer.WriteString(XRayWriter.Path, path);
                        if (label != null)
                            writer.WriteString(XRayWriter.Label, label);
                        if (lineNumber != 0)
                            writer.WriteNumber(XRayWriter.Line, lineNumber);
                        writer.WriteEndObject();
                    }
                    else
                    {
                        var idx = line.LastIndexOf(')');
                        if (idx >= 0)
                        {
                            var label = line[prefix..(idx + 1)];
                            var path = "";
                            var lineNumber = 0;

                            if (!hasStack)
                            {
                                writer.WritePropertyName(XRayWriter.Stack);
                                writer.WriteStartArray();
                                hasStack = true;
                            }

                            writer.WriteStartObject();
                            writer.WriteString(XRayWriter.Path, path);
                            if (label != null)
                                writer.WriteString(XRayWriter.Label, label);
                            writer.WriteNumber(XRayWriter.Line, lineNumber);
                            writer.WriteEndObject();
                        }
                    }
                }

                line = r.ReadLine();
            }

            if (hasStack)
            {
                writer.WriteEndArray();
            }
        }
    }
}