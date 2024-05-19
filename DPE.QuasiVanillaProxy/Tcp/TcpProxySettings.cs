﻿

using System.Text;

namespace DPE.QuasiVanillaProxy.Tcp
{
    public class TcpProxySettings
    {
        private string? _sourceTextEncoding;
        private string? _targetTextEncoding;

        public string? ProxyIPAddress { get; set; }
        public int ProxyPort { get; set; }
        public Uri? TargetUrl { get; set; }
        public int StreamBufferSize { get; set; }
        public string? ContentTypeHeader { get; set; }
        public string? SourceTextEncoding
        {
            get => _sourceTextEncoding;
            set
            {
                _sourceTextEncoding = value;
                if(!string.IsNullOrWhiteSpace(_sourceTextEncoding))
                {
                    SourceEncoding = Encoding.GetEncoding(_sourceTextEncoding);
                }
                else
                {
                    SourceEncoding = null;
                }
            }
        }
        public Encoding? SourceEncoding { private set; get; }
        public string? TargetTextEncoding
        {
            get => _targetTextEncoding;
            set
            {
                _targetTextEncoding = value;
                if (!string.IsNullOrWhiteSpace(_targetTextEncoding))
                {
                    TargetEncoding = Encoding.GetEncoding(_targetTextEncoding);
                }
                else
                {
                    TargetEncoding = null;
                }
            }
        }
        public Encoding? TargetEncoding { private set; get; }
    }
}
