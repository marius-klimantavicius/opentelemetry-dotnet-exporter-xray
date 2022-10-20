namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal static class XRayConventions
    {
        public const string StatusCodeKey = "otel.status_code";
        
        public const string AttributeEndUserId = "enduser.id";
        
        public const string AttributeNetPeerIp = "net.peer.ip";
        public const string AttributeNetPeerPort = "net.peer.port";
        public const string AttributeNetPeerName = "net.peer.name";
        public const string AttributeNetHostPort = "net.host.port";
        public const string AttributeNetHostName = "net.host.name";

        public const string AttributePeerService = "peer.service";

        public const string AttributeHttpMethod = "http.method";
        public const string AttributeHttpUrl = "http.url";
        public const string AttributeHttpTarget = "http.target";
        public const string AttributeHttpHost = "http.host";
        public const string AttributeHttpScheme = "http.scheme";
        public const string AttributeHttpStatusCode = "http.status_code";
        public const string AttributeHttpStatusText = "http.status_text";
        public const string AttributeHttpServerName = "http.server_name";
        public const string AttributeHttpClientIp = "http.client_ip";
        public const string AttributeHttpUserAgent = "http.user_agent";
        public const string AttributeHttpResponseContentLength = "http.response_content_length";

        public const string AttributeDbSystem = "db.system";
        public const string AttributeDbConnectionString = "db.connection_string";
        public const string AttributeDbUser = "db.user";
        public const string AttributeDbName = "db.name";
        public const string AttributeDbStatement = "db.statement";

        public const string AttributeRpcSystem = "rpc.system";
        public const string AttributeRpcService = "rpc.service";
        public const string AttributeRpcMethod = "rpc.method";

        public const string AttributeMessageType = "message.type";

        public const string AttributeMessagingPayloadSize = "messaging.message_payload_size_bytes";

        public const string AttributeExceptionType = "exception.type";
        public const string AttributeExceptionMessage = "exception.message";
        public const string AttributeExceptionStacktrace = "exception.stacktrace";

        public const string AttributeHostId = "host.id";
        public const string AttributeHostName = "host.name";
        public const string AttributeHostType = "host.type";
        public const string AttributeHostImageId = "host.image.id";

        public const string AttributeTelemetrySdkName = "telemetry.sdk.name";
        public const string AttributeTelemetrySdkLanguage = "telemetry.sdk.language";
        public const string AttributeTelemetrySdkVersion = "telemetry.sdk.version";
        public const string AttributeTelemetryAutoVersion = "telemetry.auto.version";

        public const string AttributeCloudProvider = "cloud.provider";
        public const string AttributeCloudAccountId = "cloud.account.id";
        public const string AttributeCloudAvailabilityZone = "cloud.availability_zone";
        public const string AttributeCloudPlatform = "cloud.platform";
        
        public const string AttributeAwsEcsContainerArn = "aws.ecs.container.arn";
        public const string AttributeAwsEcsClusterArn = "aws.ecs.cluster.arn";
        public const string AttributeAwsEcsLaunchType = "aws.ecs.launchtype";
        public const string AttributeAwsEcsTaskArn = "aws.ecs.task.arn";
        public const string AttributeAwsEcsTaskFamily = "aws.ecs.task.family";
        
        public const string AttributeK8SClusterName = "k8s.cluster.name";
        public const string AttributeK8SPodName = "k8s.pod.name";

        public const string AttributeAwsLogGroupNames = "aws.log.group.names";
        public const string AttributeAwsLogGroupArns = "aws.log.group.arns";
        public const string AttributeContainerName = "container.name";
        public const string AttributeContainerId = "container.id";
        public const string AttributeContainerImageTag = "container.image.tag";

        public const string AttributeServiceName = "service.name";
        public const string AttributeServiceNamespace = "service.namespace";
        public const string AttributeServiceInstanceId = "service.instance.id";
        public const string AttributeServiceVersion = "service.version";

        public const string AttributeAwsOperation = "aws.operation";
        public const string AttributeAwsAccount = "aws.account_id";
        public const string AttributeAwsRegion = "aws.region";
        public const string AttributeAwsRequestId = "aws.request_id";
        public const string AttributeAwsRequestId2 = "aws.requestId";
        public const string AttributeAwsQueueUrl = "aws.queue_url";
        public const string AttributeAwsQueueUrl2 = "aws.queue.url";
        public const string AttributeAwsService = "aws.service";
        public const string AttributeAwsTableName = "aws.table_name";
        public const string AttributeAwsTableName2 = "aws.table.name";

        public const string ExceptionEventName = "exception";
    }
}