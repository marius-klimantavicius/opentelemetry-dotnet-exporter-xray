using System;
using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal partial class XRayConverter
    {
        private void WriteAws(in XRayConverterContext context)
        {
            var cloud = default(string);
            var service = default(string);
            var account = default(string);
            var zone = default(string);
            var hostId = default(string);
            var hostType = default(string);
            var amiId = default(string);
            var container = default(string);
            var @namespace = default(string);
            var deployId = default(string);
            var versionLabel = default(string);
            var operation = default(string);
            var remoteRegion = default(string);
            var requestId = default(string);
            var queueUrl = default(string);
            var tableName = default(string);
            var sdk = default(string);
            var sdkName = default(string);
            var sdkLanguage = default(string);
            var sdkVersion = default(string);
            var autoVersion = default(string);
            var containerId = default(string);
            var clusterName = default(string);
            var podUid = default(string);
            var clusterArn = default(string);
            var containerArn = default(string);
            var taskArn = default(string);
            var taskFamily = default(string);
            var launchType = default(string);
            var logGroups = default(IEnumerable);
            var logGroupArns = default(IEnumerable);

            var resourceAttributes = context.ResourceAttributes;
            if (resourceAttributes.TryGetAttributeCloudProvider(out var value))
                cloud = value.AsString();

            if (resourceAttributes.TryGetAttributeCloudPlatform(out value))
                service = value.AsString();

            if (resourceAttributes.TryGetAttributeCloudAccountId(out value))
                account = value.AsString();

            if (resourceAttributes.TryGetAttributeCloudAvailabilityZone(out value))
                zone = value.AsString();

            if (resourceAttributes.TryGetAttributeHostId(out value))
                hostId = value.AsString();

            if (resourceAttributes.TryGetAttributeHostType(out value))
                hostType = value.AsString();

            if (resourceAttributes.TryGetAttributeHostImageId(out value))
                amiId = value.AsString();

            if (resourceAttributes.TryGetAttributeContainerName(out value))
                container = value.AsString();

            if (resourceAttributes.TryGetAttributeK8SPodName(out value))
                podUid = value.AsString();

            if (resourceAttributes.TryGetAttributeServiceNamespace(out value))
                @namespace = value.AsString();

            if (resourceAttributes.TryGetAttributeServiceInstanceId(out value))
                deployId = value.AsString();

            if (resourceAttributes.TryGetAttributeServiceVersion(out value))
                versionLabel = value.AsString();

            if (resourceAttributes.TryGetAttributeTelemetrySdkName(out value))
                sdkName = value.AsString();

            if (resourceAttributes.TryGetAttributeTelemetrySdkLanguage(out value))
                sdkLanguage = value.AsString();

            if (resourceAttributes.TryGetAttributeTelemetrySdkVersion(out value))
                sdkVersion = value.AsString();

            if (resourceAttributes.TryGetAttributeTelemetryAutoVersion(out value))
                autoVersion = value.AsString();

            if (resourceAttributes.TryGetAttributeContainerId(out value))
                containerId = value.AsString();

            if (resourceAttributes.TryGetAttributeK8SClusterName(out value))
                clusterName = value.AsString();

            if (resourceAttributes.TryGetAttributeAwsEcsClusterArn(out value))
                clusterArn = value.AsString();

            if (resourceAttributes.TryGetAttributeAwsEcsContainerArn(out value))
                containerArn = value.AsString();

            if (resourceAttributes.TryGetAttributeAwsEcsTaskArn(out value))
                taskArn = value.AsString();

            if (resourceAttributes.TryGetAttributeAwsEcsTaskFamily(out value))
                taskFamily = value.AsString();

            if (resourceAttributes.TryGetAttributeAwsEcsLaunchType(out value))
                launchType = value.AsString();

            if (resourceAttributes.TryGetAttributeAwsLogGroupNames(out value))
                logGroups = value as IEnumerable;

            if (resourceAttributes.TryGetAttributeAwsLogGroupArns(out value))
                logGroupArns = value as IEnumerable;

            var spanTags = context.SpanTags;
            if (spanTags.TryGetAttributeAwsOperation(out value))
                operation = value.AsString();
            else if (spanTags.TryGetAttributeRpcMethod(out value))
                operation = value.AsString();

            spanTags.TryGetAttributeRpcMethod(out value); // consume this attribute

            if (spanTags.TryGetAttributeAwsAccount(out value))
                account = value.AsString();

            if (spanTags.TryGetAttributeAwsRegion(out value))
                remoteRegion = value.AsString();

            if (spanTags.TryGetAttributeAwsRequestId(out value) || spanTags.TryGetAttributeAwsRequestId2(out value))
            {
                requestId = value.AsString();
                spanTags.TryGetAttributeAwsRequestId2(out value); // consume
            }

            if (spanTags.TryGetAttributeAwsQueueUrl(out value) || spanTags.TryGetAttributeAwsQueueUrl2(out value))
            {
                queueUrl = value.AsString();
                spanTags.TryGetAttributeAwsQueueUrl2(out value); // consume
            }

            if (spanTags.TryGetAttributeAwsTableName(out value) || spanTags.TryGetAttributeAwsTableName2(out value))
            {
                tableName = value.AsString();
                spanTags.TryGetAttributeAwsTableName2(out value); // consume
            }

            spanTags.Consume();
            if (!string.IsNullOrEmpty(cloud) && cloud != XRayAwsConventions.AttributeCloudProviderAws)
                return;

            var writer = context.Writer;
            writer.WritePropertyName(XRayWriter.Aws);
            writer.WriteStartObject();

            if (account != null)
                writer.WriteString(XRayWriter.AccountId, account);

            if (service == XRayAwsConventions.AttributeCloudPlatformAwsEc2 || !string.IsNullOrEmpty(hostId))
            {
                writer.WritePropertyName(XRayWriter.Ec2);
                writer.WriteStartObject();

                writer.WriteString(XRayWriter.InstanceId, hostId);
                writer.WriteString(XRayWriter.AvailabilityZone, zone);
                writer.WriteString(XRayWriter.InstanceSize, hostType);
                writer.WriteString(XRayWriter.AmiId, amiId);

                writer.WriteEndObject();
            }

            if (service == XRayAwsConventions.AttributeCloudPlatformAwsEcs)
            {
                writer.WritePropertyName(XRayWriter.Ecs);
                writer.WriteStartObject();

                if (container != null)
                    writer.WriteString(XRayWriter.ContainerName, container);
                if (containerId != null)
                    writer.WriteString(XRayWriter.ContainerId, containerId);
                if (zone != null)
                    writer.WriteString(XRayWriter.AvailabilityZone, zone);
                if (containerArn != null)
                    writer.WriteString(XRayWriter.ContainerArn, containerArn);
                if (clusterArn != null)
                    writer.WriteString(XRayWriter.ClusterArn, clusterArn);
                if (taskArn != null)
                    writer.WriteString(XRayWriter.TaskArn, taskArn);
                if (taskFamily != null)
                    writer.WriteString(XRayWriter.TaskFamily, taskFamily);
                if (launchType != null)
                    writer.WriteString(XRayWriter.LaunchType, launchType);

                writer.WriteEndObject();
            }

            if (service == XRayAwsConventions.AttributeCloudPlatformAwsElasticBeanstalk && !string.IsNullOrEmpty(deployId))
            {
                writer.WritePropertyName(XRayWriter.Beanstalk);
                writer.WriteStartObject();

                long.TryParse(deployId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deployNum);
                writer.WriteString(XRayWriter.Environment, @namespace);
                writer.WriteNumber(XRayWriter.DeploymentId, deployNum);
                writer.WriteString(XRayWriter.VersionLabel, versionLabel);

                writer.WriteEndObject();
            }

            if (service == XRayAwsConventions.AttributeCloudPlatformAwsEks || !string.IsNullOrEmpty(clusterName))
            {
                writer.WritePropertyName(XRayWriter.Beanstalk);
                writer.WriteStartObject();

                writer.WriteString(XRayWriter.ClusterName, clusterName);
                writer.WriteString(XRayWriter.Pod, podUid);
                writer.WriteString(XRayWriter.ContainerId, containerId);

                writer.WriteEndObject();
            }

            var hasArns = false;
            if (logGroupArns != null)
                hasArns = WriteLogGroupMetadata(writer, logGroupArns, true);
            if (!hasArns && logGroups != null)
                WriteLogGroupMetadata(writer, logGroups, false);

            if (!string.IsNullOrEmpty(sdkName) && !string.IsNullOrEmpty(sdkLanguage))
                sdk = sdkName + " for " + sdkLanguage;
            else
                sdk = sdkName;

            writer.WritePropertyName(XRayWriter.XRay);
            writer.WriteStartObject();
            if (sdk != null)
                writer.WriteString(XRayWriter.Sdk, sdk);
            if (sdkVersion != null)
                writer.WriteString(XRayWriter.SdkVersion, sdkVersion);
            writer.WriteBoolean(XRayWriter.AutoInstrumentation, !string.IsNullOrEmpty(autoVersion));
            writer.WriteEndObject();

            if (operation != null)
                writer.WriteString(XRayWriter.Operation, operation);
            if (remoteRegion != null)
                writer.WriteString(XRayWriter.RemoteRegion, remoteRegion);
            if (requestId != null)
                writer.WriteString(XRayWriter.RequestId, requestId);
            if (queueUrl != null)
                writer.WriteString(XRayWriter.QueueUrl, queueUrl);
            if (tableName != null)
                writer.WriteString(XRayWriter.TableName, tableName);

            writer.WriteEndObject();
        }

        private bool WriteLogGroupMetadata(Utf8JsonWriter writer, IEnumerable logGroups, bool isArn)
        {
            var hasLogs = false;
            foreach (var item in logGroups)
            {
                if (!hasLogs)
                {
                    writer.WritePropertyName(XRayWriter.CloudWatchLogs);
                    writer.WriteStartArray();
                    hasLogs = true;
                }

                writer.WriteStartObject();
                if (isArn)
                {
                    writer.WriteString(XRayWriter.Arn, item.AsString());
                    writer.WriteString(XRayWriter.LogGroup, ParseLogGroup(item.AsString()));
                }
                else
                {
                    writer.WriteString(XRayWriter.LogGroup, item.AsString());
                }

                writer.WriteEndObject();
            }

            if (hasLogs)
                writer.WriteEndArray();

            return hasLogs;
        }

        private string ParseLogGroup(string arn)
        {
            var span = arn.AsSpan();
            for (var i = 0; i < 7; i++)
            {
                var index = span.IndexOf(':');
                if (index < 0)
                    return arn;

                span = span.Slice(index);
            }

            var lastIndex = span.IndexOf(':');
            if (lastIndex < 0)
                return new string(span);

            return new string(span.Slice(0, lastIndex));
        }
    }
}