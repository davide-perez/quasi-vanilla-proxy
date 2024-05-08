using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPE.QuasiVanillaProxy.Udp
{
    public class UdpProxySettings
    {
        private string _textEncoding;

        public string? ProxyIPAddress { get; set; }
        public int ProxyPort { get; set; }
        public Uri? TargetUrl { get; set; }
        public string? ContentTypeHeader { get; set; }
        public string TextEncoding
        {
            get => _textEncoding;
            set
            {
                _textEncoding = value;
                Encoding = Encoding.GetEncoding(_textEncoding);
            }
        }
        public Encoding Encoding { private set; get; }
    }
}
