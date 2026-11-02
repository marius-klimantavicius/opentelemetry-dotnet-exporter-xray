using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayConverterErrorRegressionTests : XRayTest
    {
        public static IEnumerable<object[]> HttpStatuses()
        {
            foreach (var key in new[] { XRayConventions.AttributeHttpStatusCode, XRayConventions.AttributeHttpResponseStatusCode })
            foreach (var status in new[] { ActivityStatusCode.Unset, ActivityStatusCode.Ok, ActivityStatusCode.Error })
            foreach (var code in new[] { -1, 0, 200, 399, 400, 429, 499, 500, 599, 600 })
            {
                var isError = code >= 400 && code <= 499;
                var isFault = code >= 500 && code <= 599 || (!isError && status == ActivityStatusCode.Error);
                yield return new object[] { key, status, code, isError, isFault, code == 429 };
            }
        }

        [Theory]
        [MemberData(nameof(HttpStatuses))]
        public void Should_classify_http_status_independently_of_activity_status(string key, ActivityStatusCode status, int code, bool isError, bool isFault, bool isThrottle)
        {
            using var span = CreateSpan(ActivityKind.Server, status);
            span.SetTag(XRayConventions.AttributeHttpMethod, "GET");
            if (code >= 0)
                span.SetTag(key, code);

            var segment = ConvertDefault(span);

            Assert.Equal(isError, segment.IsError);
            Assert.Equal(isFault, segment.IsFault);
            Assert.Equal(isThrottle, segment.IsThrottle);
            Assert.Null(segment.Cause);

            if (code >= 0)
                Assert.Equal(code, segment.Http.Response.Status);
        }

        [Theory]
        [InlineData(429, 503, ActivityStatusCode.Ok, true, false, true)]
        [InlineData(503, 429, ActivityStatusCode.Unset, false, true, false)]
        [InlineData(200, 503, ActivityStatusCode.Unset, false, false, false)]
        [InlineData(600, 429, ActivityStatusCode.Error, false, true, false)]
        [InlineData("429", 503, ActivityStatusCode.Unset, false, false, false)]
        public void Should_prefer_legacy_http_status_even_when_invalid(object legacy, int stable, ActivityStatusCode status, bool isError, bool isFault, bool isThrottle)
        {
            foreach (var reverse in new[] { false, true })
            {
                using var span = CreateSpan(ActivityKind.Server, status);
                var tags = new Dictionary<string, object>
                {
                    [XRayConventions.AttributeHttpStatusCode] = legacy,
                    [XRayConventions.AttributeHttpResponseStatusCode] = stable,
                };

                foreach (var tag in reverse ? tags.Reverse() : tags)
                    span.SetTag(tag.Key, tag.Value);

                var segment = ConvertDefault(span);
                Assert.Equal(isError, segment.IsError);
                Assert.Equal(isFault, segment.IsFault);
                Assert.Equal(isThrottle, segment.IsThrottle);
            }
        }

        [Theory]
        [InlineData(XRayConventions.AttributeHttpStatusCode)]
        [InlineData(XRayConventions.AttributeHttpResponseStatusCode)]
        public void Should_classify_consumed_status_without_leaking_it_or_reusing_cached_flags(string key)
        {
            var converter = new XRayConverter(null, null, false, false);
            using var failed = CreateSpan(ActivityKind.Server, ActivityStatusCode.Unset);
            failed.SetTag(key, 429L);
            failed.SetTag(XRayConventions.AttributeHttpMethod, "GET");

            var segment = ConvertSegment(converter, failed);
            Assert.True(segment.IsError);
            Assert.True(segment.IsThrottle);
            Assert.False(segment.IsFault);
            Assert.Null(segment.Annotations);
            Assert.Null(segment.Metadata);

            using var healthy = CreateSpan(ActivityKind.Server, ActivityStatusCode.Unset);
            var empty = ConvertSegment(converter, healthy);
            Assert.False(empty.IsError);
            Assert.False(empty.IsFault);
            Assert.False(empty.IsThrottle);
            Assert.Null(empty.Cause);
            Assert.True(ConvertSegment(converter, failed).IsThrottle);
        }

        [Theory]
        [InlineData(ActivityStatusCode.Unset, "ERROR", true)]
        [InlineData(ActivityStatusCode.Unset, "OK", false)]
        [InlineData(ActivityStatusCode.Ok, "ERROR", false)]
        [InlineData(ActivityStatusCode.Error, "OK", true)]
        public void Should_preserve_legacy_otel_status_fallback(ActivityStatusCode status, string legacyStatus, bool isFault)
        {
            using var span = CreateSpan(ActivityKind.Server, status);
            span.SetTag(XRayConventions.StatusCodeKey, legacyStatus);
            span.SetTag(XRayConventions.AttributeHttpStatusText, "legacy status message");
            var segment = ConvertDefault(span);

            Assert.Equal(isFault, segment.IsFault);
            if (isFault)
                Assert.Equal("legacy status message", Assert.Single(segment.Cause.Exceptions).Message);
            else
                Assert.Null(segment.Cause);
        }

        [Theory]
        [InlineData(ActivityKind.Client, true)]
        [InlineData(ActivityKind.Producer, true)]
        [InlineData(ActivityKind.Server, false)]
        [InlineData(ActivityKind.Consumer, false)]
        [InlineData(ActivityKind.Internal, false)]
        public void Should_mark_exceptions_remote_for_clients_and_producers(ActivityKind kind, bool remote)
        {
            using var span = CreateSpan(kind, ActivityStatusCode.Ok);
            span.AddEvent(ExceptionEvent());

            var segment = ConvertDefault(span);
            var exception = Assert.Single(segment.Cause.Exceptions);
            Assert.Equal(remote, exception.Remote);
            Assert.False(segment.IsFault);
            Assert.Matches("^[0-9a-f]{16}$", exception.Id);
        }

        [Theory]
        [InlineData("java", ActivityKind.Client, true)]
        [InlineData("php", ActivityKind.Producer, true)]
        [InlineData("python", ActivityKind.Producer, true)]
        [InlineData("java", ActivityKind.Server, false)]
        [InlineData("python", ActivityKind.Server, false)]
        public void Should_propagate_remote_to_nested_exceptions(string language, ActivityKind kind, bool remote)
        {
            var stack = language == "python"
                ? "Traceback (most recent call last):\n  File \"test.py\", line 1, in inner\n    raise ValueError()\nValueError: inner\n\nDuring handling of the above exception, another exception occurred:\n\nTraceback (most recent call last):\n  File \"test.py\", line 2, in outer\n    raise RuntimeError()\nRuntimeError: outer\n"
                : "RuntimeException: outer\n\tat Example.outer(Example.java:2)\nCaused by: IllegalArgumentException: inner\n\tat Example.inner(Example.java:1)";
            using var span = CreateSpan(kind, ActivityStatusCode.Error);
            span.AddEvent(new ActivityEvent(XRayConventions.ExceptionEventName, tags: new ActivityTagsCollection
            {
                { XRayConventions.AttributeExceptionMessage, "outer" },
                { XRayConventions.AttributeExceptionStacktrace, stack },
            }));

            var resource = new Resource(new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkLanguage] = language,
            });

            var segment = ConvertDefault(span, resource);
            Assert.Equal(2, segment.Cause.Exceptions.Count());
            Assert.All(segment.Cause.Exceptions, exception => Assert.Equal(remote, exception.Remote));
            Assert.Equal(segment.Cause.Exceptions.ElementAt(1).Id, segment.Cause.Exceptions.ElementAt(0).Cause);
        }

        [Theory]
        [InlineData("503", ActivityStatusCode.Error, true)]
        [InlineData(503, ActivityStatusCode.Unset, false)]
        [InlineData(503L, ActivityStatusCode.Ok, false)]
        public void Should_convert_aws_http_failure_event_with_status_timestamp_and_message(object errorCode, ActivityStatusCode status, bool fault)
        {
            using var span = CreateSpan(ActivityKind.Server, status);
            span.SetTag(XRayConventions.AttributeRpcSystem, "aws-api");
            span.SetStatus(status, "status description must not replace the event");
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(1696954761).AddTicks(19).ToOffset(TimeSpan.FromHours(3));
            span.AddEvent(FailureEvent(errorCode, "service unavailable@retry", timestamp));

            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var segment = ConvertDefault(span);
                var exception = Assert.Single(segment.Cause.Exceptions);
                Assert.Equal("aws.http.error.event", exception.Type);
                Assert.True(exception.Remote);
                Assert.Equal("503@1696954761.000001@service unavailable@retry", exception.Message);
                Assert.Matches("^[0-9a-f]{16}$", exception.Id);
                Assert.Null(exception.Stack);
                Assert.Equal(fault, segment.IsFault);
                Assert.False(segment.IsError);
                Assert.False(segment.IsThrottle);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Theory]
        [InlineData(9, "0.000000")]
        [InlineData(-1, "-0.000001")]
        [InlineData(-10, "-0.000001")]
        public void Should_truncate_retry_event_time_to_epoch_microseconds(long ticks, string expectedTime)
        {
            using var span = CreateSpan(ActivityKind.Client, ActivityStatusCode.Unset);
            span.SetTag(XRayConventions.AttributeRpcSystem, "aws-api");
            span.AddEvent(FailureEvent(503, "retry", DateTimeOffset.UnixEpoch.AddTicks(ticks)));

            Assert.Equal("503@" + expectedTime + "@retry", Assert.Single(ConvertDefault(span).Cause.Exceptions).Message);
        }

        [Theory]
        [InlineData(null, ActivityStatusCode.Unset)]
        [InlineData("http", ActivityStatusCode.Unset)]
        [InlineData("AWS-API", ActivityStatusCode.Unset)]
        [InlineData("aws-api", ActivityStatusCode.Unset)]
        [InlineData("http", ActivityStatusCode.Error)]
        public void Should_gate_aws_http_failure_events_on_rpc_system(string rpcSystem, ActivityStatusCode status)
        {
            using var span = CreateSpan(ActivityKind.Client, status);
            if (rpcSystem != null)
                span.SetTag(XRayConventions.AttributeRpcSystem, rpcSystem);

            span.SetStatus(status, "fallback");
            span.AddEvent(FailureEvent(503, "retry", DateTimeOffset.UtcNow));
            var segment = ConvertDefault(span);

            if (rpcSystem == "aws-api")
                Assert.Equal("aws.http.error.event", Assert.Single(segment.Cause.Exceptions).Type);
            else if (status == ActivityStatusCode.Error)
                Assert.Equal("fallback", Assert.Single(segment.Cause.Exceptions).Message);
            else
                Assert.Null(segment.Cause);
        }

        [Theory]
        [InlineData(XRayConventions.AttributeHttpResponseStatusCode)]
        [InlineData(XRayConventions.AttributeAwsHttpErrorMessage)]
        public void Should_ignore_aws_failure_events_missing_required_attributes(string missing)
        {
            using var span = CreateSpan(ActivityKind.Client, ActivityStatusCode.Error);
            span.SetTag(XRayConventions.AttributeRpcSystem, "aws-api");
            span.SetStatus(ActivityStatusCode.Error, "must not replace malformed event");

            var tags = new ActivityTagsCollection
            {
                { XRayConventions.AttributeHttpResponseStatusCode, 503 },
                { XRayConventions.AttributeHttpStatusCode, 503 },
                { XRayConventions.AttributeAwsHttpErrorMessage, "retry" },
            };
            tags.Remove(missing);
            span.AddEvent(new ActivityEvent(XRayConventions.HttpRequestFailureEventName, tags: tags));

            var segment = ConvertDefault(span);
            Assert.NotNull(segment.Cause);
            Assert.Empty(segment.Cause.Exceptions);
        }

        [Fact]
        public void Should_keep_status_message_precedence_and_omit_remote_from_status_only_causes()
        {
            using var span = CreateSpan(ActivityKind.Client, ActivityStatusCode.Error);
            span.SetStatus(ActivityStatusCode.Error, "status description");
            span.SetTag(XRayConventions.AttributeHttpStatusText, "http description");
            var converter = new XRayConverter(null, null, false, false);

            var segment = ConvertSegment(converter, span);
            var exception = Assert.Single(segment.Cause.Exceptions);
            Assert.Equal("status description", exception.Message);
            Assert.Null(exception.Remote);
            Assert.Null(segment.Metadata);
        }

        [Fact]
        public void Should_preserve_exception_and_retry_event_order_and_reset_cached_cause()
        {
            using var span = CreateSpan(ActivityKind.Producer, ActivityStatusCode.Unset);
            span.SetTag(XRayConventions.AttributeRpcSystem, "aws-api");
            span.AddEvent(FailureEvent(429, "first", DateTimeOffset.FromUnixTimeSeconds(1)));
            span.AddEvent(ExceptionEvent());
            span.AddEvent(FailureEvent("503", "last", DateTimeOffset.FromUnixTimeSeconds(2)));
            var converter = CreateDefaultConverter();
            var segment = ConvertSegment(converter, span);

            Assert.Collection(segment.Cause.Exceptions,
                exception => Assert.Equal("429@1.000000@first", exception.Message),
                exception => Assert.Equal("exception", exception.Message),
                exception => Assert.Equal("503@2.000000@last", exception.Message));
            Assert.All(segment.Cause.Exceptions, exception => Assert.True(exception.Remote));
            Assert.Equal(3, segment.Cause.Exceptions.Select(exception => exception.Id).Distinct().Count());
            Assert.False(segment.IsError);
            Assert.False(segment.IsThrottle);

            using var empty = CreateSpan(ActivityKind.Server, ActivityStatusCode.Unset);
            Assert.Null(ConvertSegment(converter, empty).Cause);
            Assert.Equal(3, ConvertSegment(converter, span).Cause.Exceptions.Count());
        }

        private Activity CreateSpan(ActivityKind kind, ActivityStatusCode status)
        {
            var parent = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span = ActivitySource.CreateActivity("Test", kind, parent);
            Assert.NotNull(span);
            span.SetStatus(status);
            return span;
        }

        private static ActivityEvent ExceptionEvent()
        {
            return new ActivityEvent(XRayConventions.ExceptionEventName, tags: new ActivityTagsCollection
            {
                { XRayConventions.AttributeExceptionType, "TestException" },
                { XRayConventions.AttributeExceptionMessage, "exception" },
            });
        }

        private static ActivityEvent FailureEvent(object status, string message, DateTimeOffset timestamp)
        {
            return new ActivityEvent(XRayConventions.HttpRequestFailureEventName, timestamp, new ActivityTagsCollection
            {
                { XRayConventions.AttributeHttpResponseStatusCode, status },
                { XRayConventions.AttributeAwsHttpErrorMessage, message },
            });
        }
    }
}