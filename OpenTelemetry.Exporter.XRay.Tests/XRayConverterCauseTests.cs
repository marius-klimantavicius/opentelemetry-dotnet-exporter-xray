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

        // PORT: Port python later
        // PORT: Port javascript later

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
            var stacktrace = @"System.Exception: test
	at integration_test_app.Controllers.AppController.OutgoingHttp() in /Users/bhautip/Documents/otel-dotnet/aws-otel-dotnet/integration-test-app/integration-test-app/Controllers/AppController.cs:line 21
	at lambda_method(Closure , Object , Object[] )
	at Microsoft.Extensions.Internal.ObjectMethodExecutor.Execute(Object target, Object[] parameters)
	at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.SyncObjectResultExecutor.Execute(IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments)
	at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeActionMethodAsync>g__Logged|12_1(ControllerActionInvoker invoker)
	at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeNextActionFilterAsync>g__Awaited|10_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
	at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Rethrow(ActionExecutedContextSealed context)
	at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)
	at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeInnerFilterAsync()
	--- End of stack trace from previous location where exception was thrown ---
	at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|19_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
	at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Logged|17_1(ResourceInvoker invoker)
	at Microsoft.AspNetCore.Routing.EndpointMiddleware.<Invoke>g__AwaitRequestTask|6_0(Endpoint endpoint, Task requestTask, ILogger logger)
	at Microsoft.AspNetCore.Authorization.AuthorizationMiddleware.Invoke(HttpContext context)
	at Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware.Invoke(HttpContext context)";

            var parsedExceptions = ParseException(exceptionType, message, stacktrace, "dotnet");
            Assert.Collection(parsedExceptions,
                exception =>
                {
                    Assert.NotEmpty(exception.Id);
                    Assert.Equal(exceptionType, exception.Type);
                    Assert.Equal(message, exception.Message);

                    var stack = exception.Stack.ToList();
                    Assert.Equal(14, stack.Count);
                    Assert.Equal("integration_test_app.Controllers.AppController.OutgoingHttp()", stack[0].Label);
                    Assert.Equal("/Users/bhautip/Documents/otel-dotnet/aws-otel-dotnet/integration-test-app/integration-test-app/Controllers/AppController.cs", stack[0].Path);
                    Assert.Equal(21, stack[0].Line);
                    Assert.Equal("Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|19_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)", stack[9].Label);
                    Assert.Equal("", stack[9].Path);
                    Assert.Equal(0, stack[9].Line.GetValueOrDefault());
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

        // PORT: Port php later
        // PORT: Port go later
        
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