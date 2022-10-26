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

                writer.WritePropertyName(XRayField.Cause);
                writer.WriteStartObject();

                writer.WritePropertyName(XRayField.Exceptions);
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

                    writer.WritePropertyName(XRayField.Cause);
                    writer.WriteStartObject();

                    writer.WritePropertyName(XRayField.Exceptions);
                    writer.WriteStartArray();

                    writer.WriteStartObject();

                    writer.WriteString(XRayField.Id, hexId);
                    writer.WriteString(XRayField.Message, message);

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
                    if (code >= 400 && code <= 499)
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

            writer.WriteBoolean(XRayField.Error, isError);
            writer.WriteBoolean(XRayField.Throttle, isThrottle);
            writer.WriteBoolean(XRayField.Fault, isFault);
        }

        private void WriteException(Utf8JsonWriter writer, string exceptionType, string message, string stacktrace, string language)
        {
            writer.WriteStartObject();
            writer.WriteString(XRayField.Id, NewSegmentId().ToHexString());
            if (exceptionType != null)
                writer.WriteString(XRayField.Type, exceptionType);
            if (message != null)
                writer.WriteString(XRayField.Message, message);

            if (!string.IsNullOrEmpty(stacktrace))
            {
                // Right now we only care about dotnet, other languages can be ported on-demand
                switch (language)
                {
                    case "java":
                        FillJavaStacktrace(writer, stacktrace);
                        break;
                    // case "python":
                    //case "javascript":
                    case "dotnet":
                        FillDotNetStacktrace(writer, stacktrace);
                        break;
                    // case "php":
                    // case "go"
                }
            }

            writer.WriteEndObject();
        }

        private void FillJavaStacktrace(Utf8JsonWriter writer, string stacktrace)
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
                if (line.StartsWith("\tat "))
                {
                    var parenIndex = line.IndexOf('(');
                    if (parenIndex >= 0 && line[line.Length - 1] == ')')
                    {
                        var label = line[4..parenIndex];
                        var slashIdx = label.IndexOf('/');
                        if (slashIdx >= 0)
                            label = label[(slashIdx + 1)..];

                        var path = line[(parenIndex + 1)..^1];
                        var lineNumber = 0;
                        var colonIdx = path.IndexOf(':');

                        if (colonIdx >= 0)
                        {
                            var lineStr = path[(colonIdx + 1)..];
                            path = path[..colonIdx];
                            int.TryParse(lineStr, NumberStyles.Any, CultureInfo.InvariantCulture, out lineNumber);
                        }

                        hasStack = WriteExceptionStack(writer, hasStack, path, label, lineNumber);
                    }
                }
                else if (line.StartsWith("Caused by: "))
                {
                    var causeType = line[11..];
                    var colonIdx = causeType.IndexOf(':');
                    var causeMessage = "";
                    if (colonIdx >= 0)
                    {
                        causeMessage = causeType[(colonIdx + 2)..];
                        causeType = causeType[..colonIdx];
                    }

                    while (true)
                    {
                        line = r.ReadLine();
                        if (line == null)
                            break;

                        if (line.StartsWith("\tat ") && line.IndexOf('(') >= 0 && line[^1] == ')')
                            break;

                        causeMessage += line;
                    }

                    if (hasStack)
                        writer.WriteEndArray();
                    hasStack = false;

                    var nextId = NewSegmentId().ToHexString();
                    writer.WriteString(XRayField.Cause, nextId);
                    writer.WriteEndObject(); // end previous exception

                    writer.WriteStartObject();
                    writer.WriteString(XRayField.Id, nextId);
                    if (causeType != null)
                        writer.WriteString(XRayField.Type, causeType);
                    if (causeMessage != null)
                        writer.WriteString(XRayField.Message, causeMessage);

                    continue;
                }

                line = r.ReadLine();
            }

            if (hasStack)
            {
                writer.WriteEndArray();
            }
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
                line = line.Trim();
                if (line.StartsWith("at ", StringComparison.Ordinal))
                {
                    var index = line.IndexOf(" in ", StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        var parts = line.Split(" in ");
                        var label = parts[0]["at ".Length..];
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

                        hasStack = WriteExceptionStack(writer, hasStack, path, label, lineNumber);
                    }
                    else
                    {
                        var idx = line.LastIndexOf(')');
                        if (idx >= 0)
                        {
                            var label = line["at ".Length..(idx + 1)];
                            hasStack = WriteExceptionStack(writer, hasStack, "", label, 0);
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

        private bool WriteExceptionStack(Utf8JsonWriter writer, bool hasStack, string path, string label, int lineNumber)
        {
            if (!hasStack)
            {
                writer.WritePropertyName(XRayField.Stack);
                writer.WriteStartArray();
            }

            writer.WriteStartObject();
            if (path != null)
                writer.WriteString(XRayField.Path, path);
            if (label != null)
                writer.WriteString(XRayField.Label, label);
            if (lineNumber != 0)
                writer.WriteNumber(XRayField.Line, lineNumber);
            writer.WriteEndObject();

            return true;
        }
    }
}