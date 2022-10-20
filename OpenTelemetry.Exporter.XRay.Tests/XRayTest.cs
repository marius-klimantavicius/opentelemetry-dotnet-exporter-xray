using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    internal class XRayTest
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

        private static readonly ActivitySource _activitySource = new ActivitySource("test");

        public static XRayConverter CreateDefaultConverter()
        {
            return new XRayConverter(Enumerable.Empty<string>(), true, false);
        }

        public static Resource ConstructDefaultResource()
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
    }
}