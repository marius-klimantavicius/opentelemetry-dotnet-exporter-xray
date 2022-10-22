using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayConverterServiceTests : XRayTest
    {
        [Fact]
        public void Should_contain_version()
        {
            var resource = ConstructDefaultResource();
            var activity = new Activity("Test");

            var segment = ConvertDefault(activity, resource);
            Assert.Equal("semver:1.1.4", segment?.Service.Version);
        }

        [Fact]
        public void Should_use_default_version_if_missing()
        {
            var resource = ConstructDefaultResource();
            resource = new Resource(resource.Attributes.Where(s => s.Key != XRayConventions.AttributeServiceVersion));
            var activity = new Activity("Test");

            var segment = ConvertDefault(activity, resource);
            Assert.Equal("v1", segment?.Service.Version);
        }

        [Fact]
        public void Should_not_contain_service()
        {
            var activity = new Activity("Test");

            var segment = ConvertDefault(activity);
            Assert.NotNull(segment);
            Assert.Null(segment.Service);
        }
    }
}