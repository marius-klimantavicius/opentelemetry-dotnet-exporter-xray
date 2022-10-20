using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using OpenTelemetry.Exporter.XRay.Implementation;
using OpenTelemetry.Exporter.XRay.Tests.Model;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayConverterServiceTests
    {
        [Fact]
        public void Should_contain_version()
        {
            var resource = XRayTest.ConstructDefaultResource();
            var activity = new Activity("Test");
            var converter = XRayTest.CreateDefaultConverter();

            var segmentDocument = converter.Convert(resource, activity);
            var segment = JsonSerializer.Deserialize<XRaySegment>(segmentDocument);
            Assert.Equal("semver:1.1.4", segment?.Service.Version);
        }

        [Fact]
        public void Should_use_default_version_if_missing()
        {
            var resource = XRayTest.ConstructDefaultResource();
            resource = new Resource(resource.Attributes.Where(s => s.Key != XRayConventions.AttributeServiceVersion));
            var activity = new Activity("Test");
            var converter = XRayTest.CreateDefaultConverter();

            var segmentDocument = converter.Convert(resource, activity);
            var segment = JsonSerializer.Deserialize<XRaySegment>(segmentDocument);
            Assert.Equal("v1", segment?.Service.Version);
        }

        [Fact]
        public void Should_not_contain_service()
        {
            var resource = Resource.Empty;
            var activity = new Activity("Test");
            var converter = XRayTest.CreateDefaultConverter();

            var segmentDocument = converter.Convert(resource, activity);
            var segment = JsonSerializer.Deserialize<XRaySegment>(segmentDocument);
            Assert.NotNull(segment);
            Assert.Null(segment.Service);
        }
    }
}