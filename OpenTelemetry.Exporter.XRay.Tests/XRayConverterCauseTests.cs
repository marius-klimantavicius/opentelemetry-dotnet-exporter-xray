using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Exporter.XRay.Tests.Model;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayConverterCauseTests : XRayTest
    {
        [Fact]
        public void Should_map_cause_with_exception()
        {
            var errorMsg = "this is a test";
            var span = ConstructExceptionServerSpan(new Dictionary<string, object>(), ActivityStatusCode.Error, errorMsg);

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeExceptionType] = "java.lang.IllegalStateException",
                [XRayConventions.AttributeExceptionMessage] = "bad state",
                [XRayConventions.AttributeExceptionStacktrace] = @"java.lang.IllegalStateException: state is not legal
	at io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException(RecordEventsReadableSpanTest.java:626)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)
Caused by: java.lang.IllegalArgumentException: bad argument",
            };

            var ev = new ActivityEvent(XRayConventions.ExceptionEventName, span.StartTimeUtc, new ActivityTagsCollection(attributes));
            span.AddEvent(ev);

            attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeExceptionType] = "EmptyError",
            };
            ev = new ActivityEvent(XRayConventions.ExceptionEventName, span.StartTimeUtc, new ActivityTagsCollection(attributes));
            span.AddEvent(ev);

            var resource = new Resource(new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkLanguage] = "java",
            });
            var segment = ConvertDefault(span, resource);
            Assert.True(segment.IsFault);
            Assert.False(segment.IsError);
            Assert.False(segment.IsThrottle);
            Assert.NotNull(segment.Cause);

            var cause = segment.Cause;
            Assert.Equal(3, cause.Exceptions.Count());
            Assert.Collection(cause.Exceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("java.lang.IllegalStateException", exception.Type);
                    Assert.Equal("bad state", exception.Message);
                    Assert.Equal(3, exception.Stack.Count);
                },
                exception =>
                {
                    Assert.Equal(exception.Id, cause.Exceptions.ElementAt(0).Cause);
                    Assert.Equal("java.lang.IllegalArgumentException", exception.Type);
                },
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("EmptyError", exception.Type);
                    Assert.Empty(exception.Message);
                });
        }

        [Theory]
        [InlineData(ActivityStatusCode.Unset)]
        [InlineData(ActivityStatusCode.Ok)]
        public void Should_map_cause_without_error(ActivityStatusCode statusCode)
        {
            var errorMsg = "this is a test";
            var exceptionStack = @"java.lang.IllegalStateException: state is not legal
	at io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException(RecordEventsReadableSpanTest.java:626)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)
Caused by: java.lang.IllegalArgumentException: bad argument";

            var span = ConstructExceptionServerSpan(new Dictionary<string, object>(), statusCode, errorMsg);

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeExceptionType] = "java.lang.IllegalStateException",
                [XRayConventions.AttributeExceptionMessage] = "bad state",
                [XRayConventions.AttributeExceptionStacktrace] = exceptionStack,
            };

            var ev = new ActivityEvent(XRayConventions.ExceptionEventName, span.StartTimeUtc, new ActivityTagsCollection(attributes));
            span.AddEvent(ev);

            var resource = new Resource(new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkLanguage] = "java",
            });
            var segment = ConvertDefault(span, resource);

            Assert.False(segment.IsFault);
            Assert.False(segment.IsError);
            Assert.False(segment.IsThrottle);
            Assert.NotNull(segment.Cause);

            var cause = segment.Cause;
            Assert.Equal(2, cause.Exceptions.Count());

            var exception = cause.Exceptions.ElementAt(0);
            Assert.NotEmpty(exception.Id);
            Assert.Equal("java.lang.IllegalStateException", exception.Type);
            Assert.Equal("bad state", exception.Message);
            Assert.Equal(3, exception.Stack.Count);
        }

        [Theory]
        [InlineData(ActivityStatusCode.Unset)]
        [InlineData(ActivityStatusCode.Ok)]
        public void Should_map_cause_without_exception_without_error(ActivityStatusCode statusCode)
        {
            var errorMsg = "this is a test";
            var span = ConstructExceptionServerSpan(new Dictionary<string, object>(), statusCode, errorMsg);

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
            };

            var ev = new ActivityEvent("NotException", span.StartTimeUtc, new ActivityTagsCollection(attributes));
            span.AddEvent(ev);

            var resource = new Resource(new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkLanguage] = "java",
            });
            var segment = ConvertDefault(span, resource);

            Assert.False(segment.IsFault);
            Assert.False(segment.IsError);
            Assert.False(segment.IsThrottle);
            Assert.Null(segment.Cause);
        }

        [Fact]
        public void Should_map_cause_with_status_message()
        {
            var errorMsg = "this is a test";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.com/widgets",
                [XRayConventions.AttributeHttpStatusCode] = 500,
            };

            var span = ConstructExceptionServerSpan(attributes, ActivityStatusCode.Error, errorMsg);
            var segment = ConvertDefault(span);

            Assert.True(segment.IsFault);
            Assert.False(segment.IsError);
            Assert.False(segment.IsThrottle);
            Assert.NotNull(segment.Cause);
            Assert.Contains(segment.Cause.Exceptions, exception => exception.Message == errorMsg);
        }

        [Fact]
        public void Should_map_cause_with_http_status_message()
        {
            var errorMsg = "this is a test";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.com/widgets",
                [XRayConventions.AttributeHttpStatusCode] = 500,
                [XRayConventions.AttributeHttpStatusText] = errorMsg,
            };

            var span = ConstructExceptionServerSpan(attributes, ActivityStatusCode.Error);
            var segment = ConvertDefault(span);

            Assert.True(segment.IsFault);
            Assert.False(segment.IsError);
            Assert.False(segment.IsThrottle);
            Assert.NotNull(segment.Cause);
            Assert.Contains(segment.Cause.Exceptions, exception => exception.Message == errorMsg);
        }

        [Fact]
        public void Should_map_null_cause_with_zero_status_message()
        {
            var errorMsg = "this is a test";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.com/widgets",
                [XRayConventions.AttributeHttpStatusCode] = 500,
                [XRayConventions.AttributeHttpStatusText] = errorMsg,
            };

            // Status is used to determine whether an error or not.
            // This span illustrates incorrect instrumentation,
            // marking a success status with an error http status code, and status wins.
            // We do not expect to see such spans in practice.
            var span = ConstructExceptionServerSpan(attributes, ActivityStatusCode.Unset);
            var segment = ConvertDefault(span);

            Assert.False(segment.IsFault);
            Assert.False(segment.IsError);
            Assert.False(segment.IsThrottle);
            Assert.Null(segment.Cause);
        }

        [Fact]
        public void Should_map_cause_with_client_error_message()
        {
            var errorMsg = "this is a test";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.com/widgets",
                [XRayConventions.AttributeHttpStatusCode] = 499,
                [XRayConventions.AttributeHttpStatusText] = errorMsg,
            };

            var span = ConstructExceptionServerSpan(attributes, ActivityStatusCode.Error);
            var segment = ConvertDefault(span);

            Assert.True(segment.IsError);
            Assert.False(segment.IsFault);
            Assert.False(segment.IsThrottle);
            Assert.NotNull(segment.Cause);
        }

        [Fact]
        public void Should_map_cause_with_throttled_error()
        {
            var errorMsg = "this is a test";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeHttpMethod] = "POST",
                [XRayConventions.AttributeHttpUrl] = "https://api.example.com/widgets",
                [XRayConventions.AttributeHttpStatusCode] = 429,
                [XRayConventions.AttributeHttpStatusText] = errorMsg,
            };

            var span = ConstructExceptionServerSpan(attributes, ActivityStatusCode.Error);
            var segment = ConvertDefault(span);

            Assert.True(segment.IsError);
            Assert.False(segment.IsFault);
            Assert.True(segment.IsThrottle);
            Assert.NotNull(segment.Cause);
        }

        [Fact]
        public void Should_parse_exception_without_stacktrace()
        {
            var exceptionType = "com.foo.Exception";
            var message = "Error happened";
            var stacktrace = "";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Null(exception.Stack);
                });
        }

        [Fact]
        public void Should_parse_exception_without_message()
        {
            var exceptionType = "com.foo.Exception";
            var message = "";
            var stacktrace = "";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Empty(exception.Message);
                    Assert.Null(exception.Stack);
                });
        }

        [Fact]
        public void Should_parse_exception_with_java_stacktrace_no_cause()
        {
            var exceptionType = "com.foo.Exception";
            var message = "Error happened";
            var stacktrace = @"java.lang.IllegalStateException: state is not legal
	at io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException(RecordEventsReadableSpanTest.java:626)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "java");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException", s.Label);
                            Assert.Equal("RecordEventsReadableSpanTest.java", s.Path);
                            Assert.Equal(626, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke0", s.Label);
                            Assert.Equal("Native Method", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke", s.Label);
                            Assert.Equal("NativeMethodAccessorImpl.java", s.Path);
                            Assert.Equal(62, s.Line);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_stacktrace_not_java()
        {
            var exceptionType = "com.foo.Exception";
            var message = "Error happened";
            var stacktrace = @"java.lang.IllegalStateException: state is not legal
	at io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException(RecordEventsReadableSpanTest.java:626)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Null(exception.Stack);
                });
        }

        [Fact]
        public void Should_parse_exception_with_java_stacktrace_and_cause_without_stacktrace()
        {
            var exceptionType = "com.foo.Exception";
            var message = "Error happened";
            var stacktrace = @"java.lang.IllegalStateException: state is not legal
	at io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException(RecordEventsReadableSpanTest.java:626)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)
Caused by: java.lang.IllegalArgumentException: bad argument";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "java").ToList();
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException", s.Label);
                            Assert.Equal("RecordEventsReadableSpanTest.java", s.Path);
                            Assert.Equal(626, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke0", s.Label);
                            Assert.Equal("Native Method", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke", s.Label);
                            Assert.Equal("NativeMethodAccessorImpl.java", s.Path);
                            Assert.Equal(62, s.Line);
                        });
                }, exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exception.Id, parsedExceptions[0].Cause);
                    Assert.Equal("java.lang.IllegalArgumentException", exception.Type);
                    Assert.Equal("bad argument", exception.Message);
                    Assert.Null(exception.Stack);
                });
        }

        [Fact]
        public void Should_parse_exception_with_java_stacktrace_and_cause_without_message_or_stacktrace()
        {
            var exceptionType = "com.foo.Exception";
            var message = "Error happened";
            var stacktrace = @"java.lang.IllegalStateException: state is not legal
	at io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException(RecordEventsReadableSpanTest.java:626)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)
Caused by: java.lang.IllegalArgumentException";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "java").ToList();
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException", s.Label);
                            Assert.Equal("RecordEventsReadableSpanTest.java", s.Path);
                            Assert.Equal(626, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke0", s.Label);
                            Assert.Equal("Native Method", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke", s.Label);
                            Assert.Equal("NativeMethodAccessorImpl.java", s.Path);
                            Assert.Equal(62, s.Line);
                        });
                }, exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exception.Id, parsedExceptions[0].Cause);
                    Assert.Equal("java.lang.IllegalArgumentException", exception.Type);
                    Assert.Empty(exception.Message);
                    Assert.Null(exception.Stack);
                });
        }

        [Fact]
        public void Should_parse_exception_with_java_stacktrace_and_cause_with_stacktrace()
        {
            var exceptionType = "com.foo.Exception";
            var message = "Error happened";
            var stacktrace = @"java.lang.IllegalStateException: state is not legal
	at io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException(RecordEventsReadableSpanTest.java:626)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)
Caused by: java.lang.IllegalArgumentException: bad argument
	at org.junit.platform.engine.support.hierarchical.ThrowableCollector.execute(ThrowableCollector.java:73)
	at org.junit.platform.engine.support.hierarchical.NodeTestTask.executeRecursively(NodeTestTask.java)";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "java").ToList();
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException", s.Label);
                            Assert.Equal("RecordEventsReadableSpanTest.java", s.Path);
                            Assert.Equal(626, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke0", s.Label);
                            Assert.Equal("Native Method", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke", s.Label);
                            Assert.Equal("NativeMethodAccessorImpl.java", s.Path);
                            Assert.Equal(62, s.Line);
                        });
                }, exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exception.Id, parsedExceptions[0].Cause);
                    Assert.Equal("java.lang.IllegalArgumentException", exception.Type);
                    Assert.Equal("bad argument", exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("org.junit.platform.engine.support.hierarchical.ThrowableCollector.execute", s.Label);
                            Assert.Equal("ThrowableCollector.java", s.Path);
                            Assert.Equal(73, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("org.junit.platform.engine.support.hierarchical.NodeTestTask.executeRecursively", s.Label);
                            Assert.Equal("NodeTestTask.java", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_java_stacktrace_and_cause_with_stacktrace_skip_common_and_compressed_and_malformed()
        {
            var exceptionType = "com.foo.Exception";
            var message = "Error happened";
            var stacktrace = @"java.lang.IllegalStateException: state is not legal
	at io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException(RecordEventsReadableSpanTest.java:626)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke0(Native Method)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62)afaefaef
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke
	at java.base/jdk.internal.reflect.NativeMethodAccessorImpl.invoke(NativeMethodAccessorImpl.java:62
	at java.base/java.util.ArrayList.forEach(ArrayList.java:)
	Suppressed: Resource$CloseFailException: Resource ID = 2
		at Resource.close(Resource.java:26)	
		at Foo3.main(Foo3.java:5)
	Suppressed: Resource$CloseFailException: Resource ID = 1
		at Resource.close(Resource.java:26)
		at Foo3.main(Foo3.java:5)
Caused by: java.lang.IllegalArgumentException: bad argument
	at org.junit.platform.engine.support.hierarchical.ThrowableCollector.execute(ThrowableCollector.java:73)
	at org.junit.platform.engine.support.hierarchical.NodeTestTask.executeRecursively(NodeTestTask.java)
	... 99 more";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "java").ToList();
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("io.opentelemetry.sdk.trace.RecordEventsReadableSpanTest.recordException", s.Label);
                            Assert.Equal("RecordEventsReadableSpanTest.java", s.Path);
                            Assert.Equal(626, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke0", s.Label);
                            Assert.Equal("Native Method", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        },
                        s =>
                        {
                            Assert.Equal("jdk.internal.reflect.NativeMethodAccessorImpl.invoke", s.Label);
                            Assert.Equal("NativeMethodAccessorImpl.java", s.Path);
                            Assert.Equal(62, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("java.util.ArrayList.forEach", s.Label);
                            Assert.Equal("ArrayList.java", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        });
                }, exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exception.Id, parsedExceptions[0].Cause);
                    Assert.Equal("java.lang.IllegalArgumentException", exception.Type);
                    Assert.Equal("bad argument", exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("org.junit.platform.engine.support.hierarchical.ThrowableCollector.execute", s.Label);
                            Assert.Equal("ThrowableCollector.java", s.Path);
                            Assert.Equal(73, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("org.junit.platform.engine.support.hierarchical.NodeTestTask.executeRecursively", s.Label);
                            Assert.Equal("NodeTestTask.java", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_python_stacktrace_no_cause()
        {
            var exceptionType = "TypeError";
            var message = "must be str, not int";
            // We ignore the exception type / message from the stacktrace
            var stacktrace = @"Traceback (most recent call last):
  File ""main.py"", line 14, in <module>
    greet_many(['Chad', 'Dan', 1])
  File ""greetings.py"", line 12, in greet_many
    print('hi, ' + person)
TypeError: must be str, not int";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "python");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("TypeError", exception.Type);
                    Assert.Equal("must be str, not int", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("greet_many", s.Label);
                            Assert.Equal("greetings.py", s.Path);
                            Assert.Equal(12, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("<module>", s.Label);
                            Assert.Equal("main.py", s.Path);
                            Assert.Equal(14, s.Line);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_python_stacktrace_and_cause()
        {
            var exceptionType = "TypeError";
            var message = "must be str, not int";
            // We ignore the exception type / message from the stacktrace
            var stacktrace = @"Traceback (most recent call last):
  File ""bar.py"", line 10, in greet_many
    greet(person)
  File ""foo.py"", line 5, in greet
    print(greeting + ', ' + who_to_greet(someone))
ValueError: bad value

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File ""main.py"", line 14, in <module>
    greet_many(['Chad', 'Dan', 1])
  File ""greetings.py"", line 12, in greet_many
    print('hi, ' + person)
TypeError: must be str, not int";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "python");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("TypeError", exception.Type);
                    Assert.Equal("must be str, not int", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("greet_many", s.Label);
                            Assert.Equal("greetings.py", s.Path);
                            Assert.Equal(12, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("<module>", s.Label);
                            Assert.Equal("main.py", s.Path);
                            Assert.Equal(14, s.Line);
                        });
                },
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("ValueError", exception.Type);
                    Assert.Equal("bad value", exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("greet", s.Label);
                            Assert.Equal("foo.py", s.Path);
                            Assert.Equal(5, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("greet_many", s.Label);
                            Assert.Equal("bar.py", s.Path);
                            Assert.Equal(10, s.Line);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_python_stacktrace_and_multiline_cause()
        {
            var exceptionType = "TypeError";
            var message = "must be str, not int";
            // We ignore the exception type / message from the stacktrace
            var stacktrace = @"Traceback (most recent call last):
  File ""bar.py"", line 10, in greet_many
    greet(person)
  File ""foo.py"", line 5, in greet
    print(greeting + ', ' + who_to_greet(someone))
ValueError: bad value
with more on
new lines

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File ""main.py"", line 14, in <module>
    greet_many(['Chad', 'Dan', 1])
  File ""greetings.py"", line 12, in greet_many
    print('hi, ' + person)
TypeError: must be str, not int";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "python");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("TypeError", exception.Type);
                    Assert.Equal("must be str, not int", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("greet_many", s.Label);
                            Assert.Equal("greetings.py", s.Path);
                            Assert.Equal(12, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("<module>", s.Label);
                            Assert.Equal("main.py", s.Path);
                            Assert.Equal(14, s.Line);
                        });
                },
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("ValueError", exception.Type);
                    Assert.Equal("bad value\nwith more on\nnew lines", exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("greet", s.Label);
                            Assert.Equal("foo.py", s.Path);
                            Assert.Equal(5, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("greet_many", s.Label);
                            Assert.Equal("bar.py", s.Path);
                            Assert.Equal(10, s.Line);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_python_stacktrace_malformed_lines()
        {
            var exceptionType = "TypeError";
            var message = "must be str, not int";
            // We ignore the exception type / message from the stacktrace
            var stacktrace = @"Traceback (most recent call last):
  File ""main.py"", line 14 in <module>
    greet_many(['Chad', 'Dan', 1])
  File ""main.py"", lin 14, in <module>
    greet_many(['Chad', 'Dan', 1])
  File ""main.py"", line 14, fin <module>
    greet_many(['Chad', 'Dan', 1])
  File ""greetings.py"", line 12, in greet_many
    print('hi, ' + person)
TypeError: must be str, not int";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "python");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("TypeError", exception.Type);
                    Assert.Equal("must be str, not int", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("greet_many", s.Label);
                            Assert.Equal("greetings.py", s.Path);
                            Assert.Equal(12, s.Line);
                        },
                        s =>
                        {
                            Assert.Empty(s.Label ?? "");
                            Assert.Equal("main.py", s.Path);
                            Assert.Equal(14, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("<module>", s.Label);
                            Assert.Equal("main.py", s.Path);
                            Assert.Equal(0, s.Line ?? 0);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_python_stacktrace_and_malformed_cause()
        {
            var exceptionType = "TypeError";
            var message = "must be str, not int";
            // We ignore the exception type / message from the stacktrace
            var stacktrace = @"Traceback (most recent call last):
ValueError: bad value

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File ""main.py"", line 14, in <module>
    greet_many(['Chad', 'Dan', 1])
  File ""greetings.py"", line 12, in greet_many
    print('hi, ' + person)
TypeError: must be str, not int";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "python");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("TypeError", exception.Type);
                    Assert.Equal("must be str, not int", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("greet_many", s.Label);
                            Assert.Equal("greetings.py", s.Path);
                            Assert.Equal(12, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("<module>", s.Label);
                            Assert.Equal("main.py", s.Path);
                            Assert.Equal(14, s.Line);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_python_stacktrace_and_malformed_cause_message()
        {
            var exceptionType = "TypeError";
            var message = "must be str, not int";
            // We ignore the exception type / message from the stacktrace
            var stacktrace = @"Traceback (most recent call last):
  File ""bar.py"", line 10, in greet_many
    greet(person)
  File ""foo.py"", line 5, in greet
    print(greeting + ', ' + who_to_greet(someone))
ValueError bad value

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File ""main.py"", line 14, in <module>
    greet_many(['Chad', 'Dan', 1])
  File ""greetings.py"", line 12, in greet_many
    print('hi, ' + person)
TypeError: must be str, not int";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "python");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("TypeError", exception.Type);
                    Assert.Equal("must be str, not int", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("greet_many", s.Label);
                            Assert.Equal("greetings.py", s.Path);
                            Assert.Equal(12, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("<module>", s.Label);
                            Assert.Equal("main.py", s.Path);
                            Assert.Equal(14, s.Line);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_javascript_stacktrace()
        {
            var exceptionType = "TypeError";
            var message = "Cannot read property 'value' of null";
            // We ignore the exception type / message from the stacktrace
            var stacktrace = @"TypeError: Cannot read property 'value' of null
    at speedy (/home/gbusey/file.js:6:11)
    at makeFaster (/home/gbusey/file.js:5:3)
    at Object.<anonymous> (/home/gbusey/file.js:10:1)
    at node.js:906:3
    at Array.forEach (native)
    at native";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "javascript");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.Equal("TypeError", exception.Type);
                    Assert.Equal("Cannot read property 'value' of null", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("speedy ", s.Label);
                            Assert.Equal("/home/gbusey/file.js", s.Path);
                            Assert.Equal(6, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("makeFaster ", s.Label);
                            Assert.Equal("/home/gbusey/file.js", s.Path);
                            Assert.Equal(5, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("Object.<anonymous> ", s.Label);
                            Assert.Equal("/home/gbusey/file.js", s.Path);
                            Assert.Equal(10, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("", s.Label ?? "");
                            Assert.Equal("node.js", s.Path);
                            Assert.Equal(906, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("Array.forEach ", s.Label);
                            Assert.Equal("native", s.Path);
                            Assert.Equal(0, s.Line ?? 0);
                        },
                        s =>
                        {
                            Assert.Equal("", s.Label ?? "");
                            Assert.Equal("native", s.Path);
                            Assert.Equal(0, s.Line ?? 0);
                        }
                    );
                });
        }

        [Fact]
        public void Should_parse_exception_with_stacktrace_not_javascript()
        {
            var exceptionType = "TypeError";
            var message = "Cannot read property 'value' of null";
            var stacktrace = @"TypeError: Cannot read property 'value' of null
    at speedy (/home/gbusey/file.js:6:11)
    at makeFaster (/home/gbusey/file.js:5:3)
    at Object.<anonymous> (/home/gbusey/file.js:10:1)";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Null(exception.Stack);
                });
        }

        [Fact]
        public void Should_parse_exception_with_javascript_stacktrace_malformed_lines()
        {
            var exceptionType = "TypeError";
            var message = "Cannot read property 'value' of null";
            // We ignore the exception type / message from the stacktrace
            var stacktrace = @"TypeError: Cannot read property 'value' of null
    at speedy (/home/gbusey/file.js)
    at makeFaster (/home/gbusey/file.js:5:3)malformed123
    at Object.<anonymous> (/home/gbusey/file.js:10";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "javascript");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.Equal("TypeError", exception.Type);
                    Assert.Equal("Cannot read property 'value' of null", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("speedy ", s.Label);
                            Assert.Equal("/home/gbusey/file.js", s.Path);
                            Assert.Equal(0, s.Line ?? 0);
                        }
                    );
                });
        }

        [Fact]
        public void Should_parse_exception_with_simple_stacktrace_dotnet()
        {
            var exceptionType = "System.FormatException";
            var message = "Input string was not in a correct format";
            var stacktrace = @"System.FormatException: Input string was not in a correct format.
	at System.Number.ThrowOverflowOrFormatException(ParsingStatus status, TypeCode type)
	at System.Number.ParseInt32(ReadOnlySpan1 value, NumberStyles styles, NumberFormatInfo info)
	at System.Int32.Parse(String s)
	at MyNamespace.IntParser.Parse(String s) in C:\apps\MyNamespace\IntParser.cs:line 11
	at MyNamespace.Program.Main(String[] args) in C:\apps\MyNamespace\Program.cs:line 12";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "dotnet");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("System.Number.ThrowOverflowOrFormatException(ParsingStatus status, TypeCode type)", s.Label);
                            Assert.Equal("", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        },
                        s =>
                        {
                            Assert.Equal("System.Number.ParseInt32(ReadOnlySpan1 value, NumberStyles styles, NumberFormatInfo info)", s.Label);
                            Assert.Equal("", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        },
                        s =>
                        {
                            Assert.Equal("System.Int32.Parse(String s)", s.Label);
                            Assert.Equal("", s.Path);
                            Assert.Equal(0, s.Line.GetValueOrDefault());
                        },
                        s =>
                        {
                            Assert.Equal("MyNamespace.IntParser.Parse(String s)", s.Label);
                            Assert.Equal("C:\\apps\\MyNamespace\\IntParser.cs", s.Path);
                            Assert.Equal(11, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("MyNamespace.Program.Main(String[] args)", s.Label);
                            Assert.Equal("C:\\apps\\MyNamespace\\Program.cs", s.Path);
                            Assert.Equal(12, s.Line);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_inner_exception_stacktrace_dotnet()
        {
            var exceptionType = "System.Exception";
            var message = "test";
            var stacktrace = "System.Exception: Second exception happened\r\n" +
                    " ---> System.Exception: Error happened when get weatherforecasts\r\n" +
                    "   at TestAppApi.Services.ForecastService.GetWeatherForecasts() in D:\\Users\\foobar\\test-app\\TestAppApi\\Services\\ForecastService.cs:line 9\r\n" +
                    "   --- End of inner exception stack trace ---\r\n" +
                    "   at TestAppApi.Services.ForecastService.GetWeatherForecasts() in D:\\Users\\foobar\\test-app\\TestAppApi\\Services\\ForecastService.cs:line 12\r\n" +
                    "   at TestAppApi.Controllers.WeatherForecastController.Get() in D:\\Users\\foobar\\test-app\\TestAppApi\\Controllers\\WeatherForecastController.cs:line 31"
                ;

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "dotnet");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    var stack = exception.Stack.ToList();
                    Assert.Equal(3, stack.Count);
                    Assert.Equal("TestAppApi.Services.ForecastService.GetWeatherForecasts()", stack[0].Label);
                    Assert.Equal("D:\\Users\\foobar\\test-app\\TestAppApi\\Services\\ForecastService.cs", stack[0].Path);
                    Assert.Equal(9, stack[0].Line);
                    Assert.Equal("TestAppApi.Controllers.WeatherForecastController.Get()", stack[2].Label);
                    Assert.Equal("D:\\Users\\foobar\\test-app\\TestAppApi\\Controllers\\WeatherForecastController.cs", stack[2].Path);
                    Assert.Equal(31, stack[2].Line);
                });
        }

        [Fact]
        public void Should_parse_exception_with_malformed_stacktrace_dotnet()
        {
            var exceptionType = "System.Exception";
            var message = "test";
            var stacktrace = @"System.Exception: test
	at integration_test_app.Controllers.AppController.OutgoingHttp() in /Users/bhautip/Documents/otel-dotnet/aws-otel-dotnet/integration-test-app/integration-test-app/Controllers/AppController.cs:line 21
	at Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware.Invoke(HttpContext context malformed
	at System.Net.Http.HttpConnectionPool.ConnectAsync(HttpRequestMessage request, Boolean allowHttp2, CancellationToken cancellationToken) non-malformed";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "dotnet");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    var stack = exception.Stack.ToList();
                    Assert.Equal(2, stack.Count);
                    Assert.Equal("integration_test_app.Controllers.AppController.OutgoingHttp()", stack[0].Label);
                    Assert.Equal("/Users/bhautip/Documents/otel-dotnet/aws-otel-dotnet/integration-test-app/integration-test-app/Controllers/AppController.cs", stack[0].Path);
                    Assert.Equal(21, stack[0].Line);
                    Assert.Equal("System.Net.Http.HttpConnectionPool.ConnectAsync(HttpRequestMessage request, Boolean allowHttp2, CancellationToken cancellationToken)", stack[1].Label);
                    Assert.Equal("", stack[1].Path);
                    Assert.Equal(0, stack[1].Line.GetValueOrDefault());
                });
        }

        [Fact]
        public void Should_parse_exception_with_php_stacktrace()
        {
            var exceptionType = "Exception";
            var message = "Thrown from grandparent";

            var stacktrace = @"Exception: Thrown from grandparent
	at grandparent_func(test.php:56)
	at parent_func(test.php:51)
	at child_func(test.php:44)
	at main(test.php:63)";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "php");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("grandparent_func", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(56, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("parent_func", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(51, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("child_func", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(44, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("main", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(63, s.Line);
                        });
                });

        }

        [Fact]
        public void Should_parse_exception_without_php_stacktrace()
        {
            var exceptionType = "Exception";
            var message = "Thrown from grandparent";

            var stacktrace = @"";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "php");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Null(exception.Stack);
                });
        }

        [Fact]
        public void Should_parse_exception_with_php_stacktrace_with_cause()
        {
            var exceptionType = "Exception";
            var message = "Thrown from class B";

            var stacktrace = @"Exception: Thrown from class B
	at B.exc(test.php:59)
	at fail(test.php:81)
	at main(test.php:89)
Caused by: Exception: Thrown from class A";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "php").ToList();
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Equal(exception.Cause, parsedExceptions[1].Id);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("B.exc", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(59, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("fail", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(81, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("main", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(89, s.Line);
                        });
                },
                exception =>
                {
                    Assert.Equal("Exception", exception.Type);
                    Assert.Equal("Thrown from class A", exception.Message);
                    Assert.Null(exception.Stack);
                });
        }

        [Fact]
        public void Should_parse_exception_with_php_stacktrace_with_cause_and_stacktrace()
        {
            var exceptionType = "Exception";
            var message = "Thrown from class B";

            var stacktrace = @"Exception: Thrown from class B
	at B.exc(test.php:59)
	at fail(test.php:81)
	at main(test.php:89)
Caused by: Exception: Thrown from class A
	at A.exc(test.php:48)
	at B.exc(test.php:56)
	... 2 more";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "php").ToList();
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Equal(exception.Cause, parsedExceptions[1].Id);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("B.exc", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(59, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("fail", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(81, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("main", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(89, s.Line);
                        });
                },
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("Exception", exception.Type);
                    Assert.Equal("Thrown from class A", exception.Message);
                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("A.exc", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(48, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("B.exc", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(56, s.Line);
                        });
                });
        }

        [Fact]
        public void Should_parse_exception_with_php_stacktrace_with_multiple_cause()
        {
            var exceptionType = "Exception";
            var message = "Thrown from class C";

            var stacktrace = @"Exception: Thrown from class C
	at C.exc(test.php:74)
	at main(test.php:89)
Caused by: Exception: Thrown from class B
	at B.exc(test.php:59)
	at C.exc(test.php:71)
	... 3 more
Caused by: Exception: Thrown from class A
	at A.exc(test.php:48)
	at B.exc(test.php:56)
	... 4 more";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "php").ToList();
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Equal(exception.Cause, parsedExceptions[1].Id);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("C.exc", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(74, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("main", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(89, s.Line);
                        });
                },
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("Exception", exception.Type);
                    Assert.Equal("Thrown from class B", exception.Message);
                    Assert.Equal(exception.Cause, parsedExceptions[2].Id);

                    Assert.Equal(2, exception.Stack.Count);
                    Assert.Equal("B.exc", exception.Stack[0].Label);
                    Assert.Equal("test.php", exception.Stack[0].Path);
                    Assert.Equal(59, exception.Stack[0].Line);
                },
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal("Exception", exception.Type);
                    Assert.Equal("Thrown from class A", exception.Message);

                    Assert.Equal(2, exception.Stack.Count);
                    Assert.Equal("B.exc", exception.Stack[1].Label);
                    Assert.Equal("test.php", exception.Stack[1].Path);
                    Assert.Equal(56, exception.Stack[1].Line);
                });
        }

        [Fact]
        public void Should_parse_exception_with_php_stacktrace_with_malformed_lines()
        {
            var exceptionType = "Exception";
            var message = "Thrown from class B";

            var stacktrace = @"Exception: Thrown from class B
	at B.exc(test.php:59)
	at fail(test.php:81 malformed
	at main(test.php:89)";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "php").ToList();
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.Equal("B.exc", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(59, s.Line);
                        },
                        s =>
                        {
                            Assert.Equal("main", s.Label);
                            Assert.Equal("test.php", s.Path);
                            Assert.Equal(89, s.Line);
                        });
                });
        }
        
        [Fact]
        public void Should_parse_exception_with_go_without_stacktrace()
        {
            var exceptionType = "Exception";
            var message = "Thrown from grandparent";

            var stacktrace = @"";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "go");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);
                    Assert.Null(exception.Stack);
                });
        }
        
        [Fact]
        public void Should_parse_exception_with_go_with_stacktrace()
        {
            var exceptionType = "Exception";
            var message = "error message";

            var stacktrace = @"goroutine 19 [running]:
go.opentelemetry.io/otel/sdk/trace.recordStackTrace(0x0, 0x0)
	otel-go-core/opentelemetry-go/sdk/trace/span.go:323 +0x9b
go.opentelemetry.io/otel/sdk/trace.(*span).RecordError(0xc0003a6000, 0x14a5f00, 0xc00038c000, 0xc000390140, 0x3, 0x4)
	otel-go-core/opentelemetry-go/sdk/trace/span.go:302 +0x3fc
go.opentelemetry.io/otel/sdk/trace.TestRecordErrorWithStackTrace(0xc000102900)
	otel-go-core/opentelemetry-go/sdk/trace/trace_test.go:1167 +0x3ef
testing.tRunner(0xc000102900, 0x1484410)
	/usr/local/Cellar/go/1.16.3/libexec/src/testing/testing.go:1193 +0x1a3
created by testing.(*T).Run
	/usr/local/Cellar/go/1.16.3/libexec/src/testing/testing.go:1238 +0x63c";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "go");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Collection(exception.Stack,
                        s =>
                        {
                            Assert.StartsWith("go.opentelemetry.io/otel/sdk/trace.recordStackTrace", s.Label);
                            Assert.Equal("otel-go-core/opentelemetry-go/sdk/trace/span.go", s.Path);
                            Assert.Equal(323, s.Line);
                        },
                        s =>
                        {
                            Assert.StartsWith("go.opentelemetry.io/otel/sdk/trace.(*span).RecordError", s.Label);
                            Assert.Equal("otel-go-core/opentelemetry-go/sdk/trace/span.go", s.Path);
                            Assert.Equal(302, s.Line);
                        },
                        s =>
                        {
                        },
                        s =>
                        {
                            Assert.StartsWith("testing.tRunner", s.Label);
                            Assert.Equal("/usr/local/Cellar/go/1.16.3/libexec/src/testing/testing.go", s.Path);
                            Assert.Equal(1193, s.Line);
                        },
                        s =>
                        {
                            Assert.StartsWith("created by testing.(*T).Run", s.Label);
                            Assert.Equal("/usr/local/Cellar/go/1.16.3/libexec/src/testing/testing.go", s.Path);
                            Assert.Equal(1238, s.Line);
                        });
                });
        }
        
        [Fact]
        public void Should_parse_multiple_exception_with_go_stacktrace()
        {
            var exceptionType = "Exception";
            var message = "panic";

            var stacktrace = @"goroutine 19 [running]:
go.opentelemetry.io/otel/sdk/trace.recordStackTrace(0x0, 0x0)
	Documents/otel-go-core/opentelemetry-go/sdk/trace/span.go:318 +0x9b
go.opentelemetry.io/otel/sdk/trace.(*span).End(0xc000082300, 0xc0000a0040, 0x1, 0x1)
	Documents/otel-go-core/opentelemetry-go/sdk/trace/span.go:252 +0x4ee
panic(0x1414f00, 0xc0000a0050)
	/usr/local/Cellar/go/1.16.3/libexec/src/runtime/panic.go:971 +0x4c7
go.opentelemetry.io/otel/sdk/trace.TestSpanCapturesPanicWithStackTrace.func1()
	Documents/otel-go-core/opentelemetry-go/sdk/trace/trace_test.go:1425 +0x225
github.com/stretchr/testify/assert.didPanic.func1(0xc0001ad0e8, 0xc0001ad0d7, 0xc0001ad0d8, 0xc00009e048)
	go/pkg/mod/github.com/stretchr/testify@v1.7.0/assert/assertions.go:1018 +0xb8
github.com/stretchr/testify/assert.didPanic(0xc00009e048, 0x14a5b00, 0x0, 0x0, 0x0, 0x0)
	go/pkg/mod/github.com/stretchr/testify@v1.7.0/assert/assertions.go:1020 +0x85
github.com/stretchr/testify/assert.PanicsWithError(0x14a5b60, 0xc000186600, 0x146e31c, 0xd, 0xc00009e048, 0x0, 0x0, 0x0, 0xc000038900)
	go/pkg/mod/github.com/stretchr/testify@v1.7.0/assert/assertions.go:1071 +0x10c
goroutine 26 [running]:
github.com/stretchr/testify/require.PanicsWithError(0x14a7328, 0xc000186600, 0x146e31c, 0xd, 0xc00009e048, 0x0, 0x0, 0x0)
	go/pkg/mod/github.com/stretchr/testify@v1.7.0/require/require.go:1607 +0x15e
go.opentelemetry.io/otel/sdk/trace.TestSpanCapturesPanicWithStackTrace(0xc000186600)
	Documents/otel-go-core/opentelemetry-go/sdk/trace/trace_test.go:1427 +0x33a
testing.tRunner(0xc000186600, 0x1484440)
	/usr/local/Cellar/go/1.16.3/libexec/src/testing/testing.go:1193 +0x1a3
created by testing.(*T).Run
	/usr/local/Cellar/go/1.16.3/libexec/src/testing/testing.go:1238 +0x63c";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "go");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    Assert.Equal(11, exception.Stack.Count);
                    
                    Assert.StartsWith("go.opentelemetry.io/otel/sdk/trace.recordStackTrace", exception.Stack[0].Label);
                    Assert.Equal("Documents/otel-go-core/opentelemetry-go/sdk/trace/span.go", exception.Stack[0].Path);
                    Assert.Equal(318, exception.Stack[0].Line);
                    Assert.StartsWith("github.com/stretchr/testify/require.PanicsWithError", exception.Stack[7].Label);
                    Assert.Equal("go/pkg/mod/github.com/stretchr/testify@v1.7.0/require/require.go", exception.Stack[7].Path);
                    Assert.Equal(1607, exception.Stack[7].Line);
                    Assert.StartsWith("go.opentelemetry.io/otel/sdk/trace.TestSpanCapturesPanicWithStackTrace", exception.Stack[8].Label);
                    Assert.Equal("Documents/otel-go-core/opentelemetry-go/sdk/trace/trace_test.go", exception.Stack[8].Path);
                    Assert.Equal(1427, exception.Stack[8].Line);
                    Assert.StartsWith("created by testing.(*T).Run", exception.Stack[10].Label);
                    Assert.Equal("/usr/local/Cellar/go/1.16.3/libexec/src/testing/testing.go", exception.Stack[10].Path);
                    Assert.Equal(1238, exception.Stack[10].Line);

                });
        }

        private IEnumerable<XRayException> ParseException(string exceptionType, string message, string stacktrace, string language)
        {
            var span = ConstructExceptionServerSpan(new Dictionary<string, object>(), ActivityStatusCode.Error);

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeExceptionType] = exceptionType,
                [XRayConventions.AttributeExceptionMessage] = message,
                [XRayConventions.AttributeExceptionStacktrace] = stacktrace,
            };

            var ev = new ActivityEvent(XRayConventions.ExceptionEventName, span.StartTimeUtc, new ActivityTagsCollection(attributes));
            span.AddEvent(ev);

            var resource = new Resource(new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkLanguage] = language,
            });

            var segment = ConvertDefault(span, resource);
            Assert.NotNull(segment.Cause);
            Assert.NotNull(segment.Cause.Exceptions);

            return segment.Cause.Exceptions;
        }

        private Activity ConstructExceptionServerSpan(IEnumerable<KeyValuePair<string, object>> attributes, ActivityStatusCode statusCode, string errorMessage = null)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddSeconds(-90);

            var activityContext = new ActivityContext(XRayTraceId.Generate(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var activity = ActivitySource.CreateActivity("/widgets", ActivityKind.Server, activityContext, attributes);
            Assert.NotNull(activity);

            activity.SetStartTime(startTime);
            activity.SetEndTime(endTime);
            activity.SetStatus(statusCode, errorMessage);

            return activity;
        }
    }
}