using System;
using System.Collections.Generic;
using OpenTelemetry.Exporter.XRay.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.XRay.Tests
{
    public class XRayTagDictionaryTests
    {
        [Fact]
        public void Should_enumerate_extra_item()
        {
            var dictionary = new XRayTagDictionary();
            dictionary.Initialize(new[]
            {
                new KeyValuePair<string, object>(XRayConventions.AttributeDbName, "1"),
                new KeyValuePair<string, object>(XRayConventions.AttributeDbUser, "2"),
                new KeyValuePair<string, object>(XRayConventions.AttributeNetHostName, "3"),
                new KeyValuePair<string, object>(XRayConventions.AttributeNetHostPort, "4"),
                new KeyValuePair<string, object>(XRayConventions.AttributeCloudProvider, "5"),
                new KeyValuePair<string, object>("some", "6"),
            });

            var actual = new Dictionary<string, object>();
            foreach (var item in dictionary)
                actual.Add(item.Key, item.Value);

            Assert.Equal(6, actual.Count);
            Assert.Equal("6", actual["some"]);
        }
    }
}