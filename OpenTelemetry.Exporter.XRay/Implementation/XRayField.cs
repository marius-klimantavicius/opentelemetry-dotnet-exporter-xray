using System.Text.Json;

namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal static class XRayField
    {
        public static readonly JsonEncodedText Name = JsonEncodedText.Encode("name");
        public static readonly JsonEncodedText Id = JsonEncodedText.Encode("id");
        public static readonly JsonEncodedText StartTime = JsonEncodedText.Encode("start_time");

        public static readonly JsonEncodedText Service = JsonEncodedText.Encode("service");
        public static readonly JsonEncodedText Origin = JsonEncodedText.Encode("origin");
        public static readonly JsonEncodedText User = JsonEncodedText.Encode("user");
        public static readonly JsonEncodedText ResourceArn = JsonEncodedText.Encode("resource_arn");

        public static readonly JsonEncodedText TraceId = JsonEncodedText.Encode("trace_id");
        public static readonly JsonEncodedText EndTime = JsonEncodedText.Encode("end_time");
        public static readonly JsonEncodedText InProgress = JsonEncodedText.Encode("in_progress");
        public static readonly JsonEncodedText Http = JsonEncodedText.Encode("http");
        public static readonly JsonEncodedText Fault = JsonEncodedText.Encode("fault");
        public static readonly JsonEncodedText Error = JsonEncodedText.Encode("error");
        public static readonly JsonEncodedText Throttle = JsonEncodedText.Encode("throttle");
        public static readonly JsonEncodedText Cause = JsonEncodedText.Encode("cause");
        public static readonly JsonEncodedText Aws = JsonEncodedText.Encode("aws");
        public static readonly JsonEncodedText Annotations = JsonEncodedText.Encode("annotations");
        public static readonly JsonEncodedText Metadata = JsonEncodedText.Encode("metadata");
        public static readonly JsonEncodedText Subsegments = JsonEncodedText.Encode("subsegments");

        public static readonly JsonEncodedText Namespace = JsonEncodedText.Encode("namespace");
        public static readonly JsonEncodedText ParentId = JsonEncodedText.Encode("parent_id");
        public static readonly JsonEncodedText Type = JsonEncodedText.Encode("type");
        public static readonly JsonEncodedText PrecursorIds = JsonEncodedText.Encode("precursor_ids");
        public static readonly JsonEncodedText Traced = JsonEncodedText.Encode("traced");
        public static readonly JsonEncodedText Sql = JsonEncodedText.Encode("sql");

        // HTTPData
        public static readonly JsonEncodedText Request = JsonEncodedText.Encode("request");
        public static readonly JsonEncodedText Response = JsonEncodedText.Encode("response");

        // Request
        public static readonly JsonEncodedText XForwardedFor = JsonEncodedText.Encode("x_forwarded_for");
        public static readonly JsonEncodedText Method = JsonEncodedText.Encode("method");
        public static readonly JsonEncodedText Url = JsonEncodedText.Encode("url");
        public static readonly JsonEncodedText UserAgent = JsonEncodedText.Encode("user_agent");
        public static readonly JsonEncodedText ClientIp = JsonEncodedText.Encode("client_ip");

        // Response
        public static readonly JsonEncodedText Status = JsonEncodedText.Encode("status");
        public static readonly JsonEncodedText ContentLength = JsonEncodedText.Encode("content_length");

        // Cause
        public static readonly JsonEncodedText WorkingDirectory = JsonEncodedText.Encode("working_directory");
        public static readonly JsonEncodedText Paths = JsonEncodedText.Encode("paths");
        public static readonly JsonEncodedText Exceptions = JsonEncodedText.Encode("exceptions");

        // Exception
        public static readonly JsonEncodedText Message = JsonEncodedText.Encode("message");
        public static readonly JsonEncodedText Remote = JsonEncodedText.Encode("remote");
        public static readonly JsonEncodedText Truncated = JsonEncodedText.Encode("truncated");
        public static readonly JsonEncodedText Skipped = JsonEncodedText.Encode("skipped");
        public static readonly JsonEncodedText Stack = JsonEncodedText.Encode("stack");

        // StackFrame
        public static readonly JsonEncodedText Path = JsonEncodedText.Encode("path");
        public static readonly JsonEncodedText Line = JsonEncodedText.Encode("line");
        public static readonly JsonEncodedText Label = JsonEncodedText.Encode("label");

        // AWS
        public static readonly JsonEncodedText Beanstalk = JsonEncodedText.Encode("elastic_beanstalk");
        public static readonly JsonEncodedText CloudWatchLogs = JsonEncodedText.Encode("cloudwatch_logs");
        public static readonly JsonEncodedText Ecs = JsonEncodedText.Encode("ecs");
        public static readonly JsonEncodedText Ec2 = JsonEncodedText.Encode("ec2");
        public static readonly JsonEncodedText Eks = JsonEncodedText.Encode("eks");
        public static readonly JsonEncodedText XRay = JsonEncodedText.Encode("xray");
        public static readonly JsonEncodedText AccountId = JsonEncodedText.Encode("account_id");
        public static readonly JsonEncodedText Operation = JsonEncodedText.Encode("operation");
        public static readonly JsonEncodedText RemoteRegion = JsonEncodedText.Encode("region");
        public static readonly JsonEncodedText RequestId = JsonEncodedText.Encode("request_id");
        public static readonly JsonEncodedText QueueUrl = JsonEncodedText.Encode("queue_url");
        public static readonly JsonEncodedText TableName = JsonEncodedText.Encode("table_name");
        public static readonly JsonEncodedText Retries = JsonEncodedText.Encode("retries");

        // EC2
        public static readonly JsonEncodedText InstanceId = JsonEncodedText.Encode("instance_id");
        public static readonly JsonEncodedText AvailabilityZone = JsonEncodedText.Encode("availability_zone");
        public static readonly JsonEncodedText InstanceSize = JsonEncodedText.Encode("instance_size");
        public static readonly JsonEncodedText AmiId = JsonEncodedText.Encode("ami_id");

        // ECS
        public static readonly JsonEncodedText ContainerName = JsonEncodedText.Encode("container");
        public static readonly JsonEncodedText ContainerId = JsonEncodedText.Encode("container_id");
        public static readonly JsonEncodedText TaskArn = JsonEncodedText.Encode("task_arn");
        public static readonly JsonEncodedText TaskFamily = JsonEncodedText.Encode("task_family");
        public static readonly JsonEncodedText ClusterArn = JsonEncodedText.Encode("cluster_arn");
        public static readonly JsonEncodedText ContainerArn = JsonEncodedText.Encode("container_arn");
        public static readonly JsonEncodedText LaunchType = JsonEncodedText.Encode("launch_type");

        // Beanstalk
        public static readonly JsonEncodedText Environment = JsonEncodedText.Encode("environment_name");
        public static readonly JsonEncodedText VersionLabel = JsonEncodedText.Encode("version_label");
        public static readonly JsonEncodedText DeploymentId = JsonEncodedText.Encode("deployment_id");

        // EKS
        public static readonly JsonEncodedText ClusterName = JsonEncodedText.Encode("cluster_name");
        public static readonly JsonEncodedText Pod = JsonEncodedText.Encode("pod");

        // LogGroup
        public static readonly JsonEncodedText LogGroup = JsonEncodedText.Encode("log_group");
        public static readonly JsonEncodedText Arn = JsonEncodedText.Encode("arn");

        // XRay
        public static readonly JsonEncodedText Sdk = JsonEncodedText.Encode("sdk");
        public static readonly JsonEncodedText SdkVersion = JsonEncodedText.Encode("sdk_version");
        public static readonly JsonEncodedText AutoInstrumentation = JsonEncodedText.Encode("auto_instrumentation");

        // Service
        public static readonly JsonEncodedText Version = JsonEncodedText.Encode("version");
        public static readonly JsonEncodedText CompilerVersion = JsonEncodedText.Encode("compiler_version");
        public static readonly JsonEncodedText Compiler = JsonEncodedText.Encode("compiler");

        // SQL
        public static readonly JsonEncodedText ConnectionString = JsonEncodedText.Encode("connection_string");
        public static readonly JsonEncodedText SanitizedQuery = JsonEncodedText.Encode("sanitized_query");
        public static readonly JsonEncodedText DatabaseType = JsonEncodedText.Encode("database_type");
        public static readonly JsonEncodedText DatabaseVersion = JsonEncodedText.Encode("database_version");
        public static readonly JsonEncodedText DriverVersion = JsonEncodedText.Encode("driver_version");
        public static readonly JsonEncodedText Preparation = JsonEncodedText.Encode("preparation"); // "statement" / "call"

        // Metadata
        public static readonly JsonEncodedText Default = JsonEncodedText.Encode("default");
    }
}