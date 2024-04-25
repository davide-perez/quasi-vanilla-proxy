

namespace DPE.QuasiVanillaProxy.Tcp
{
    public class TcpProxySettings
    {
        public string? ProxyIPAddress { get; set; }
        public int ProxyPort { get; set; }
        public Uri? TargetUrl { get; set; }
    }
}
