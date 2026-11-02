using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayConverterAwsTests : XRayTest
    {
        [Fact]
        public void Should_map_ec2()
        {
            var instanceId = "i-00f7c0bcb26da2a99";
            var hostType = "m5.xlarge";
            var imageId = "ami-0123456789";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEc2,
                [XRayConventions.AttributeCloudAccountId] = "123456789",
                [XRayConventions.AttributeCloudAvailabilityZone] = "us-east-1c",
                [XRayConventions.AttributeHostId] = instanceId,
                [XRayConventions.AttributeHostType] = hostType,
                [XRayConventions.AttributeHostImageId] = imageId,
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.Ec2);
            Assert.Null(awsData.Ecs);
            Assert.Null(awsData.Beanstalk);
            Assert.Null(awsData.Eks);

            Assert.Equal("123456789", awsData.AccountId);
            Assert.Equal(instanceId, awsData.Ec2.InstanceId);
            Assert.Equal("us-east-1c", awsData.Ec2.AvailabilityZone);
            Assert.Equal(hostType, awsData.Ec2.InstanceSize);
            Assert.Equal(imageId, awsData.Ec2.AmiId);
        }

        [Fact]
        public void Should_map_ecs()
        {
            var instanceId = "i-00f7c0bcb26da2a99";
            var containerName = "signup_aggregator-x82ufje83";
            var containerId = "0123456789A";
            var az = "us-east-1c";
            var launchType = "fargate";
            var family = "family";
            var taskArn = "arn:aws:ecs:us-west-2:123456789123:task/123";
            var clusterArn = "arn:aws:ecs:us-west-2:123456789123:cluster/my-cluster";
            var containerArn = "arn:aws:ecs:us-west-2:123456789123:container-instance/123";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEcs,
                [XRayConventions.AttributeCloudAccountId] = "123456789",
                [XRayConventions.AttributeCloudAvailabilityZone] = az,
                [AttributeContainerImageName] = "otel/signupaggregator",
                [XRayConventions.AttributeContainerImageTag] = "v1",
                [XRayConventions.AttributeContainerName] = containerName,
                [XRayConventions.AttributeContainerId] = containerId,
                [XRayConventions.AttributeHostId] = instanceId,
                [XRayConventions.AttributeAwsEcsClusterArn] = clusterArn,
                [XRayConventions.AttributeAwsEcsContainerArn] = containerArn,
                [XRayConventions.AttributeAwsEcsTaskArn] = taskArn,
                [XRayConventions.AttributeAwsEcsTaskFamily] = family,
                [XRayConventions.AttributeAwsEcsLaunchType] = launchType,
                [XRayConventions.AttributeHostType] = "m5.xlarge",
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.Ecs);
            Assert.NotNull(awsData.Ec2);
            Assert.Null(awsData.Beanstalk);
            Assert.Null(awsData.Eks);

            Assert.Equal(containerName, awsData.Ecs.ContainerName);
            Assert.Equal(containerId, awsData.Ecs.ContainerId);
            Assert.Equal(az, awsData.Ecs.AvailabilityZone);
            Assert.Equal(clusterArn, awsData.Ecs.ClusterArn);
            Assert.Equal(containerArn, awsData.Ecs.ContainerArn);
            Assert.Equal(taskArn, awsData.Ecs.TaskArn);
            Assert.Equal(family, awsData.Ecs.TaskFamily);
            Assert.Equal(launchType, awsData.Ecs.LaunchType);
        }

        [Fact]
        public void Should_map_beanstalk()
        {
            var deployID = "232";
            var versionLabel = "4";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsElasticBeanstalk,
                [XRayConventions.AttributeCloudAccountId] = "123456789",
                [XRayConventions.AttributeCloudAvailabilityZone] = "us-east-1c",
                [XRayConventions.AttributeServiceNamespace] = "production",
                [XRayConventions.AttributeServiceInstanceId] = deployID,
                [XRayConventions.AttributeServiceVersion] = versionLabel,
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Null(awsData.Ec2);
            Assert.Null(awsData.Ecs);
            Assert.NotNull(awsData.Beanstalk);
            Assert.Null(awsData.Eks);

            Assert.Equal("production", awsData.Beanstalk.Environment);
            Assert.Equal(versionLabel, awsData.Beanstalk.VersionLabel);
            Assert.Equal(232, awsData.Beanstalk.DeploymentId);
        }

        [Fact]
        public void Should_map_eks()
        {
            var instanceID = "i-00f7c0bcb26da2a99";
            var containerName = "signup_aggregator-x82ufje83";
            var containerID = "0123456789A";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeCloudProvider] = XRayAwsConventions.AttributeCloudProviderAws,
                [XRayConventions.AttributeCloudPlatform] = XRayAwsConventions.AttributeCloudPlatformAwsEks,
                [XRayConventions.AttributeCloudAccountId] = "123456789",
                [XRayConventions.AttributeCloudAvailabilityZone] = "us-east-1c",
                [AttributeContainerImageName] = "otel/signupaggregator",
                [XRayConventions.AttributeContainerImageTag] = "v1",
                [XRayConventions.AttributeK8SClusterName] = "production",
                [AttributeK8SNamespaceName] = "default",
                [AttributeK8SDeploymentName] = "signup_aggregator",
                [XRayConventions.AttributeK8SPodName] = "my-deployment-65dcf7d447-ddjnl",
                [XRayConventions.AttributeContainerName] = containerName,
                [XRayConventions.AttributeContainerId] = containerID,
                [XRayConventions.AttributeHostId] = instanceID,
                [XRayConventions.AttributeHostType] = "m5.xlarge",
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.Eks);
            Assert.NotNull(awsData.Ec2);
            Assert.Null(awsData.Ecs);
            Assert.Null(awsData.Beanstalk);

            Assert.Equal("production", awsData.Eks.ClusterName);
            Assert.Equal("my-deployment-65dcf7d447-ddjnl", awsData.Eks.Pod);
            Assert.Equal(containerID, awsData.Eks.ContainerId);
        }

        [Fact]
        public void Should_map_sqs()
        {
            // PORT: Original test initializes attrs and not used it

            var activity = new Activity("Test");

            var queueUrl = "https://sqs.use1.amazonaws.com/Meltdown-Alerts";
            activity.SetTag(XRayConventions.AttributeAwsOperation, "SendMessage");
            activity.SetTag(XRayConventions.AttributeAwsAccount, "987654321");
            activity.SetTag(XRayConventions.AttributeAwsRegion, "us-east-2");
            activity.SetTag(XRayConventions.AttributeAwsQueueUrl, queueUrl);
            activity.SetTag("employee.id", "XB477");

            var segment = ConvertDefault(activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Equal(queueUrl, awsData.QueueUrl);
            Assert.Equal("us-east-2", awsData.RemoteRegion);
        }

        [Fact]
        public void Should_map_rpc_method_to_operation()
        {
            var activity = new Activity("Test");
            activity.SetTag(XRayConventions.AttributeRpcMethod, "ListBuckets");

            var segment = ConvertDefault(activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Equal("ListBuckets", awsData.Operation);
        }

        [Fact]
        public void Should_map_sqs_with_alternative_attribute()
        {
            var activity = new Activity("Test");

            var queueUrl = "https://sqs.use1.amazonaws.com/Meltdown-Alerts";
            activity.SetTag(XRayConventions.AttributeAwsQueueUrl2, queueUrl);

            var segment = ConvertDefault(activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Equal(queueUrl, awsData.QueueUrl);
        }

        [Fact]
        public void Should_map_sqs_with_semantic_convention_attribute()
        {
            var activity = new Activity("Test");

            var queueUrl = "https://sqs.use1.amazonaws.com/Meltdown-Alerts";
            activity.SetTag(XRayConventions.AttributeMessagingUrl, queueUrl);

            var segment = ConvertDefault(activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Equal(queueUrl, awsData.QueueUrl);
        }

        [Fact]
        public void Should_map_dynamo_db()
        {
            var activity = new Activity("Test");

            var tableName = "WIDGET_TYPES";
            activity.SetTag(XRayConventions.AttributeRpcMethod, "IncorrectAWSSDKOperation");
            activity.SetTag(XRayConventions.AttributeAwsOperation, "PutItem");
            activity.SetTag(XRayConventions.AttributeAwsRequestId, "75107C82-EC8A-4F75-883F-4440B491B0AB");
            activity.SetTag(XRayConventions.AttributeAwsTableName, tableName);

            var segment = ConvertDefault(activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Equal("PutItem", awsData.Operation);
            Assert.Equal("75107C82-EC8A-4F75-883F-4440B491B0AB", awsData.RequestId);
            Assert.Equal(tableName, awsData.TableName);
        }

        [Fact]
        public void Should_map_dynamo_db_with_alternative_attribute()
        {
            var activity = new Activity("Test");

            var tableName = "MyTable";
            activity.SetTag(XRayConventions.AttributeAwsTableName2, tableName);

            var segment = ConvertDefault(activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Equal(tableName, awsData.TableName);
        }

        [Fact]
        public void Should_map_dynamo_db_with_semantic_convention_attribute()
        {
            var activity = new Activity("Test");

            var tableName = "MyTable";
            activity.SetTag(XRayConventions.AttributeAwsDynamoDbTableNames, tableName);

            var segment = ConvertDefault(activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Equal(tableName, awsData.TableName);
        }

        [Fact]
        public void Should_map_request_id_with_alternative_attribute()
        {
            var activity = new Activity("Test");

            var requestId = "12345-request";
            activity.SetTag(XRayConventions.AttributeAwsRequestId2, requestId);

            var segment = ConvertDefault(activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.Equal(requestId, awsData.RequestId);
        }

        [Fact]
        public void Should_map_java_sdk()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkName] = "opentelemetry",
                [XRayConventions.AttributeTelemetrySdkLanguage] = "java",
                [XRayConventions.AttributeTelemetrySdkVersion] = "1.2.3",
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.XRay);
            Assert.Equal("opentelemetry for java", awsData.XRay.Sdk);
            Assert.Equal("1.2.3", awsData.XRay.SdkVersion);
        }

        [Theory]
        [InlineData(XRayConventions.AttributeTelemetryAutoVersion)]
        [InlineData(XRayConventions.AttributeTelemetryDistroVersion)]
        public void Should_map_java_sdk_autoannotation(string versionKey)
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkName] = "opentelemetry",
                [XRayConventions.AttributeTelemetrySdkLanguage] = "java",
                [XRayConventions.AttributeTelemetrySdkVersion] = "1.2.3",
                [versionKey] = "3.4.5",
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.XRay);
            Assert.Equal("opentelemetry for java", awsData.XRay.Sdk);
            Assert.Equal("1.2.3", awsData.XRay.SdkVersion);
            Assert.True(awsData.XRay.AutoInstrumentation);
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData("", "", false)]
        [InlineData("3.4.5", null, true)]
        [InlineData(null, "3.4.5", true)]
        [InlineData("3.4.5", "", true)]
        [InlineData("", "3.4.5", true)]
        [InlineData("old", "new", true)]
        public void Should_emit_auto_instrumentation_as_boolean_and_clear_cached_resource_values(string autoVersion, string distroVersion, bool expected)
        {
            var attributes = new Dictionary<string, object>();
            if (autoVersion != null)
                attributes[XRayConventions.AttributeTelemetryAutoVersion] = autoVersion;
            if (distroVersion != null)
                attributes[XRayConventions.AttributeTelemetryDistroVersion] = distroVersion;

            var converter = CreateDefaultConverter();
            var activity = new Activity("Test");
            foreach (var resourceAttributes in new[] { attributes.AsEnumerable(), attributes.Reverse() })
            {
                using var document = JsonDocument.Parse(converter.Convert(new Resource(resourceAttributes), activity));
                var value = document.RootElement.GetProperty("aws").GetProperty("xray").GetProperty("auto_instrumentation");
                Assert.Equal(expected ? JsonValueKind.True : JsonValueKind.False, value.ValueKind);
                Assert.Equal(expected, value.GetBoolean());
                Assert.False(ConvertSegment(converter, activity).Aws.XRay.AutoInstrumentation);
            }
        }

        [Theory]
        [InlineData(XRayConventions.AttributeAwsSqsQueueUrl)]
        [InlineData(XRayConventions.AttributeAwsQueueUrl)]
        [InlineData(XRayConventions.AttributeAwsQueueUrl2)]
        public void Should_consume_sqs_queue_url_alias(string key)
        {
            var activity = new Activity("Test");
            activity.SetTag(key, "https://sqs.us-east-1.amazonaws.com/123/queue");
            var converter = new XRayConverter(null, null, false, false);

            var segment = ConvertSegment(converter, activity);

            Assert.Equal("https://sqs.us-east-1.amazonaws.com/123/queue", segment.Aws.QueueUrl);
            Assert.Null(segment.Annotations);
            Assert.Null(segment.Metadata);
            Assert.Null(ConvertSegment(converter, new Activity("Empty")).Aws.QueueUrl);
            Assert.Equal(segment.Aws.QueueUrl, ConvertSegment(converter, activity).Aws.QueueUrl);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Should_prefer_messaging_url_over_sqs_alias_and_sqs_alias_over_legacy_keys(bool reverse)
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeAwsQueueUrl] = "legacy",
                [XRayConventions.AttributeAwsQueueUrl2] = "alternative",
                [XRayConventions.AttributeAwsSqsQueueUrl] = "sqs",
            };
            var activity = new Activity("Test");
            foreach (var item in reverse ? attributes.Reverse() : attributes)
                activity.SetTag(item.Key, item.Value);
            
            Assert.Equal("sqs", ConvertDefault(activity).Aws.QueueUrl);

            activity.SetTag(XRayConventions.AttributeMessagingUrl, "messaging");
            
            var segment = ConvertDefault(activity);
            Assert.Equal("messaging", segment.Aws.QueueUrl);
            // Upstream retains messaging.url in the filtered attributes.
            Assert.Equal("messaging", segment.Annotations["messaging_url"].ToString());
            activity.SetTag(XRayConventions.AttributeMessagingUrl, "");
            Assert.Equal("", ConvertDefault(activity).Aws.QueueUrl);
        }

        [Theory]
        [InlineData(false, false, "messaging")]
        [InlineData(true, false, "messaging")]
        [InlineData(true, true, "sqs")]
        public void Should_gate_messaging_url_fallback_but_always_accept_sqs_semantic_attribute(bool emitV1, bool disableV0, string expected)
        {
            var activity = new Activity("Test");
            activity.SetTag(XRayConventions.AttributeAwsSqsQueueUrl, "sqs");
            activity.SetTag(XRayConventions.AttributeMessagingUrl, "messaging");
            var converter = new XRayConverter(null, null, false, false,
                emitV1HttpNetworkConventions: emitV1, dontEmitV0HttpNetworkConventions: disableV0);

            var segment = ConvertSegment(converter, activity);
            Assert.Equal(expected, segment.Aws.QueueUrl);
            Assert.True(segment.Metadata["default"].ContainsKey(XRayConventions.AttributeMessagingUrl));
            Assert.False(segment.Metadata["default"].ContainsKey(XRayConventions.AttributeAwsSqsQueueUrl));

            activity.SetTag(XRayConventions.AttributeMessagingUrl, null);
            Assert.Equal("sqs", ConvertSegment(converter, activity).Aws.QueueUrl);
        }

        public static IEnumerable<object[]> DynamoDbTableAttributes()
        {
            yield return new object[] { "Table", null, "Table", null };
            yield return new object[] { new[] { "Table" }, null, "Table", null };
            yield return new object[] { new List<string> { "Table" }, "Legacy", "Table", null };
            yield return new object[] { new[] { "First", "Second" }, "Legacy", null, new[] { "First", "Second" } };
            yield return new object[] { Array.Empty<string>(), null, null, null };
            yield return new object[] { Array.Empty<string>(), "Legacy", "Legacy", null };
            yield return new object[] { 123, "Legacy", "Legacy", null };
            yield return new object[] { new object[] { "First", 123 }, null, null, new[] { "First", "" } };
        }

        [Theory]
        [MemberData(nameof(DynamoDbTableAttributes))]
        public void Should_map_dynamodb_scalar_single_and_multiple_tables(object names, string legacyName, string expectedName, string[] expectedNames)
        {
            var converter = new XRayConverter(null, null, false, false);
            var activity = new Activity("Test");
            activity.SetTag(XRayConventions.AttributeAwsDynamoDbTableNames, names);
            if (legacyName != null)
                activity.SetTag(XRayConventions.AttributeAwsTableName, legacyName);

            var segment = ConvertSegment(converter, activity);

            Assert.Equal(expectedName, segment.Aws.TableName);
            Assert.Equal(expectedNames, segment.Aws.TableNames);
            // DynamoDB's semantic attribute remains available for metadata, like upstream.
            Assert.True(segment.Metadata["default"].ContainsKey(XRayConventions.AttributeAwsDynamoDbTableNames));
            Assert.False(segment.Metadata["default"].ContainsKey(XRayConventions.AttributeAwsTableName));
            var empty = ConvertSegment(converter, new Activity("Empty"));
            Assert.Null(empty.Aws.TableName);
            Assert.Null(empty.Aws.TableNames);
            Assert.Null(empty.Metadata);
            Assert.Equal(expectedNames, ConvertSegment(converter, activity).Aws.TableNames);
        }

        [Fact]
        public void Should_map_go_sdk()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkName] = "opentelemetry",
                [XRayConventions.AttributeTelemetrySdkLanguage] = "go",
                [XRayConventions.AttributeTelemetrySdkVersion] = "2.0.3",
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.XRay);
            Assert.Equal("opentelemetry for go", awsData.XRay.Sdk);
            Assert.Equal("2.0.3", awsData.XRay.SdkVersion);
        }

        [Fact]
        public void Should_map_custom_sdk()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkName] = "opentracing",
                [XRayConventions.AttributeTelemetrySdkLanguage] = "java",
                [XRayConventions.AttributeTelemetrySdkVersion] = "2.0.3",
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.XRay);
            Assert.Equal("opentracing for java", awsData.XRay.Sdk);
            Assert.Equal("2.0.3", awsData.XRay.SdkVersion);
        }

        [Fact]
        public void Should_map_log_groups()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeAwsLogGroupNames] = new[] { "group1", "group2" },
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.CloudWatchLogs);
            Assert.Collection(awsData.CloudWatchLogs,
                s => Assert.Equal("group1", s.LogGroup),
                s => Assert.Equal("group2", s.LogGroup));
        }

        [Theory]
        [InlineData("group1&group2")]
        [InlineData("group1&group2&")]
        [InlineData("group1&&group2")]
        [InlineData("&group1&group2")]
        public void Should_map_log_groups_with_ampersand(string attributeValue)
        {
            var group1 = "group1";
            var group2 = "group2";

            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeAwsLogGroupNames] = attributeValue,
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.CloudWatchLogs);
            Assert.Collection(awsData.CloudWatchLogs,
                s =>
                {
                    Assert.Equal(group1, s.LogGroup);
                },
                s =>
                {
                    Assert.Equal(group2, s.LogGroup);
                });
        }

        [Fact]
        public void Should_map_log_groups_with_only_ampersand()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeAwsLogGroupNames] = "&",
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            if (awsData.CloudWatchLogs != null)
                Assert.Empty(awsData.CloudWatchLogs);
            else
                Assert.Null(awsData.CloudWatchLogs);
        }

        [Fact]
        public void Should_map_log_group_arns()
        {
            var group1 = "arn:aws:logs:us-east-1:123456789123:log-group:group1";
            var group2 = "arn:aws:logs:us-east-1:123456789123:log-group:group2:*";
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeAwsLogGroupArns] = new[] { group1, group2 },
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.CloudWatchLogs);
            Assert.Collection(awsData.CloudWatchLogs,
                s =>
                {
                    Assert.Equal(group1, s.Arn);
                    Assert.Equal("group1", s.LogGroup);
                },
                s =>
                {
                    Assert.Equal(group2, s.Arn);
                    Assert.Equal("group2", s.LogGroup);
                });
        }

        [Fact]
        public void Should_map_log_group_arns_from_string_resource()
        {
            var group1 = "arn:aws:logs:us-east-1:123456789123:log-group:group1";
            var group2 = "arn:aws:logs:us-east-1:123456789123:log-group:group2";
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeAwsLogGroupArns] = "arn:aws:logs:us-east-1:123456789123:log-group:group1&arn:aws:logs:us-east-1:123456789123:log-group:group2",
            };

            var resource = new Resource(attributes);
            var activity = new Activity("Test");
            var segment = ConvertDefault(activity, resource);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.CloudWatchLogs);
            Assert.Collection(awsData.CloudWatchLogs,
                s =>
                {
                    Assert.Equal(group1, s.Arn);
                    Assert.Equal("group1", s.LogGroup);
                },
                s =>
                {
                    Assert.Equal(group2, s.Arn);
                    Assert.Equal("group2", s.LogGroup);
                });
        }

        [Fact]
        public void Should_map_log_group_names()
        {
            var activity = new Activity("Test");
            var converter = new XRayConverter(null, null, true, false, logGroupNames: new[] { "group1", "group2" });
            var segment = ConvertSegment(converter, activity);

            Assert.NotNull(segment.Aws);

            var awsData = segment.Aws;
            Assert.NotNull(awsData.CloudWatchLogs);
            Assert.Collection(awsData.CloudWatchLogs,
                s => Assert.Equal("group1", s.LogGroup),
                s => Assert.Equal("group2", s.LogGroup));
        }
    }
}