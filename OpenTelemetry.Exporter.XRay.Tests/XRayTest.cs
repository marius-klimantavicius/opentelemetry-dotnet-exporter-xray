using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Exporter.XRay.Tests.Model;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayTest : IDisposable
    {
        public const string ResourceStringKey = "string.key";
        public const string ResourceIntKey = "int.key";
        public const string ResourceDoubleKey = "double.key";
        public const string ResourceBoolKey = "bool.key";
        public const string ResourceMapKey = "map.key";
        public const string ResourceArrayKey = "array.key";

        public const string AttributeContainerImageName = "container.image.name";
        public const string AttributeK8SNamespaceName = "k8s.namespace.name";
        public const string AttributeK8SDeploymentName = "k8s.deployment.name";
        public const string AttributeCloudRegion = "cloud.region";

        public const string AttributeMessagingMessageID = "messaging.message_id";
        public const string AttributeMessagingMessagePayloadCompressedSizeBytes = "messaging.message_payload_compressed_size_bytes";
        
        private ActivitySource _activitySource;
        private ActivityListener _activityListener;

        internal ActivitySource ActivitySource
        {
            get
            {
                if (_activitySource == null)
                {
                    _activitySource = new ActivitySource($"TestSource-{Guid.NewGuid()}");
                    _activityListener = new ActivityListener();
                    _activityListener.ShouldListenTo = source => ReferenceEquals(source, _activitySource);
                    _activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
                    ActivitySource.AddActivityListener(_activityListener);
                }

                return _activitySource;
            }
        }

        public void Dispose()
        {
            _activityListener?.Dispose();
            _activitySource?.Dispose();
        }

        internal XRaySegment ConvertSegment(XRayConverter converter, Activity span, Resource resource = null)
        {
            resource ??= Resource.Empty;
            var segmentDocument = converter.Convert(resource, span);
            var segment = JsonSerializer.Deserialize<XRaySegment>(segmentDocument);
            Assert.NotNull(segment);

            return segment;
        }

        internal XRaySegment ConvertDefault(Activity span, Resource resource = null)
        {
            resource ??= Resource.Empty;

            var converter = CreateDefaultConverter();
            var segmentDocument = converter.Convert(resource, span);
            var segment = JsonSerializer.Deserialize<XRaySegment>(segmentDocument);
            Assert.NotNull(segment);

            return segment;
        }

        internal XRaySegment ConvertDefault(Activity span, Resource resource, out string segmentDocument)
        {
            resource ??= Resource.Empty;

            var converter = CreateDefaultConverter();
            
            segmentDocument = converter.Convert(resource, span);
            
            var segment = JsonSerializer.Deserialize<XRaySegment>(segmentDocument);
            Assert.NotNull(segment);

            return segment;
        }

        internal static XRayConverter CreateDefaultConverter()
        {
            return new XRayConverter(Enumerable.Empty<string>(), true, false);
        }

        internal static Resource ConstructDefaultResource()
        {
            var resourceList = new[] { "foo", "bar" };

            var resourceAttributes = new[]
            {
                new KeyValuePair<string, object>(XRayConventions.AttributeServiceName, "signup_aggregator"),
                new KeyValuePair<string, object>(XRayConventions.AttributeServiceVersion, "semver:1.1.4"),
                new KeyValuePair<string, object>(XRayConventions.AttributeContainerName, "signup_aggregator"),
                new KeyValuePair<string, object>(AttributeContainerImageName, "otel/signupaggregator"),
                new KeyValuePair<string, object>(XRayConventions.AttributeContainerImageTag, "v1"),
                new KeyValuePair<string, object>(XRayConventions.AttributeK8SClusterName, "production"),
                new KeyValuePair<string, object>(AttributeK8SNamespaceName, "default"),
                new KeyValuePair<string, object>(AttributeK8SDeploymentName, "signup_aggregator"),
                new KeyValuePair<string, object>(XRayConventions.AttributeK8SPodName, "signup_aggregator-x82ufje83"),
                new KeyValuePair<string, object>(XRayConventions.AttributeCloudProvider, XRayAwsConventions.AttributeCloudProviderAws),
                new KeyValuePair<string, object>(XRayConventions.AttributeCloudAccountId, "123456789"),
                new KeyValuePair<string, object>(AttributeCloudRegion, "us-east-1"),
                new KeyValuePair<string, object>(XRayConventions.AttributeCloudAvailabilityZone, "us-east-1c"),
                new KeyValuePair<string, object>(ResourceStringKey, "string"),
                new KeyValuePair<string, object>(ResourceIntKey, 10),
                new KeyValuePair<string, object>(ResourceDoubleKey, 5.0),
                new KeyValuePair<string, object>(ResourceBoolKey, true),
                new KeyValuePair<string, object>(ResourceArrayKey, resourceList),
            };

            return new Resource(resourceAttributes);
        }

        internal static void ConstructTimedEventsWithReceivedMessageEvent(Activity span)
        {
            var tags = new ActivityTagsCollection(new Dictionary<string, object>
            {
                ["message.type"] = "RECEIVED",
                [AttributeMessagingMessageID] = 1,
                [AttributeMessagingMessagePayloadCompressedSizeBytes] = 6478,
                [XRayConventions.AttributeMessagingPayloadSize] = 12452,

            });
            var ev = new ActivityEvent("", span.StartTimeUtc.Add(span.Duration), tags);
            span.AddEvent(ev);
        }

        internal static void ConstructTimedEventsWithSentMessageEvent(Activity span)
        {
            var tags = new ActivityTagsCollection(new Dictionary<string, object>
            {
                ["message.type"] = "SENT",
                [AttributeMessagingMessageID] = 1,
                [XRayConventions.AttributeMessagingPayloadSize] = 7480,
            });
            var ev = new ActivityEvent("", span.StartTimeUtc.Add(span.Duration), tags);
            span.AddEvent(ev);
        }
    }
}