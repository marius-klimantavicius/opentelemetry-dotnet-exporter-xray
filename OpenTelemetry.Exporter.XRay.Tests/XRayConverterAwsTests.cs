using System.Collections.Generic;
using System.Diagnostics;
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

        [Fact]
        public void Should_map_java_sdk_autoannotation()
        {
            var attributes = new Dictionary<string, object>
            {
                [XRayConventions.AttributeTelemetrySdkName] = "opentelemetry",
                [XRayConventions.AttributeTelemetrySdkLanguage] = "java",
                [XRayConventions.AttributeTelemetrySdkVersion] = "1.2.3",
                [XRayConventions.AttributeTelemetryAutoVersion] = "3.4.5",
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