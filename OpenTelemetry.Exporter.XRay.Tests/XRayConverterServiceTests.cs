using System;
using System.Collections.Generic;
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

        public static IEnumerable<object[]> VersionAttributes()
        {
            yield return new object[] { "service", new[] { "image-first", "image-second" }, "legacy", "service" };
            yield return new object[] { "", new[] { "image-first" }, "legacy", "" };
            yield return new object[] { null, new[] { "image-first", "image-second" }, "legacy", "image-first" };
            yield return new object[] { null, new[] { "", "image-second" }, "legacy", "" };
            yield return new object[] { null, new[] { 123, 456 }, "legacy", "" };
            yield return new object[] { null, Array.Empty<string>(), "legacy", "legacy" };
            yield return new object[] { null, Array.Empty<string>(), null, null };
            yield return new object[] { null, "scalar-is-not-tags", "legacy", "legacy" };
            yield return new object[] { null, 123, "legacy", "legacy" };
            yield return new object[] { null, "scalar-is-not-tags", null, null };
            yield return new object[] { null, null, "legacy", "legacy" };
        }

        [Theory]
        [MemberData(nameof(VersionAttributes))]
        public void Should_prefer_service_version_then_first_image_tag_then_legacy_tag(string serviceVersion, object imageTags, string legacyTag, string expected)
        {
            var attributes = new Dictionary<string, object>();
            if (serviceVersion != null)
                attributes[XRayConventions.AttributeServiceVersion] = serviceVersion;

            if (imageTags != null)
                attributes[XRayConventions.AttributeContainerImageTags] = imageTags;

            if (legacyTag != null)
                attributes[XRayConventions.AttributeContainerImageTag] = legacyTag;

            foreach (var resourceAttributes in new[] { attributes.AsEnumerable(), attributes.Reverse() })
            {
                var segment = ConvertDefault(new Activity("Test"), new Resource(resourceAttributes));
                if (expected == null)
                    Assert.Null(segment.Service);
                else
                    Assert.Equal(expected, segment.Service.Version);
            }
        }

        [Fact]
        public void Should_reset_service_version_attributes_between_cached_conversions()
        {
            var converter = CreateDefaultConverter();
            var resource = new Resource(new Dictionary<string, object>
            {
                [XRayConventions.AttributeContainerImageTags] = new[] { "first", "second" },
            });
            var activity = new Activity("Test");

            Assert.Equal("first", ConvertSegment(converter, activity, resource).Service.Version);
            Assert.Null(ConvertSegment(converter, activity).Service);
            Assert.Equal("first", ConvertSegment(converter, activity, resource).Service.Version);

            var legacy = new Resource(new Dictionary<string, object>
            {
                [XRayConventions.AttributeContainerImageTag] = "legacy",
            });
            Assert.Equal("legacy", ConvertSegment(converter, activity, legacy).Service.Version);
        }
    }
}