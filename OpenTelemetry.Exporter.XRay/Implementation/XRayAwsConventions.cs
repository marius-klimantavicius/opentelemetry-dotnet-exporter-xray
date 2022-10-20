namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal class XRayAwsConventions
    {
        public const string OriginEc2 = "AWS::EC2::Instance";
        public const string OriginEcs = "AWS::ECS::Container";
        public const string OriginEcsEc2 = "AWS::ECS::EC2";
        public const string OriginEcsFargate = "AWS::ECS::Fargate";
        public const string OriginElasticBeanstalk = "AWS::ElasticBeanstalk::Environment";
        public const string OriginEks = "AWS::EKS::Container";
        public const string OriginAppRunner = "AWS::AppRunner::Service";

        public const string AttributeCloudProviderAws = "aws";

        public const string AttributeCloudPlatformAwsEc2 = "aws_ec2";
        public const string AttributeCloudPlatformAwsEcs = "aws_ecs";
        public const string AttributeCloudPlatformAwsEks = "aws_eks";
        public const string AttributeCloudPlatformAwsElasticBeanstalk = "aws_elastic_beanstalk";
        public const string AttributeCloudPlatformAwsAppRunner = "aws_app_runner";

        public const string AttributeAwsEcsLaunchTypeEc2 = "ec2";
        public const string AttributeAwsEcsLaunchTypeFargate = "fargate";
    }
}