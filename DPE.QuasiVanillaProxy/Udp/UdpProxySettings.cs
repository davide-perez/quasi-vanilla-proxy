using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPE.QuasiVanillaProxy.Udp
{
    public class UdpProxySettings
    {
        public string? ProxyIPAddress { get; set; }
        public int ProxyPort { get; set; }
        public Uri? TargetUrl { get; set; }
        public string? ContentTypeHeader { get; set; }
    }
}
