namespace OpenTelemetry.Exporter.XRay.Implementation
{
    internal struct XRayHttpUrlParts
    {
        public XRayHttpUrlPartValue AttributeHttpUrl;
        public XRayHttpUrlPartValue AttributeHttpScheme;
        public XRayHttpUrlPartValue AttributeHttpHost;
        public XRayHttpUrlPartValue AttributeHttpTarget;
        public XRayHttpUrlPartValue AttributeHttpServerName;
        public XRayHttpUrlPartValue AttributeNetHostPort;
        public XRayHttpUrlPartValue AttributeHostName;
        public XRayHttpUrlPartValue AttributeNetHostName;
        public XRayHttpUrlPartValue AttributeNetPeerName;
        public XRayHttpUrlPartValue AttributeNetPeerPort;
        public XRayHttpUrlPartValue AttributeNetPeerIp;
        public XRayHttpUrlPartValue AttributeUrlPath;
        public XRayHttpUrlPartValue AttributeUrlQuery;
        public XRayHttpUrlPartValue AttributeServerAddress;
        public XRayHttpUrlPartValue AttributeServerPort;
        public XRayHttpUrlPartValue AttributeNetworkPeerPort;
    }
}