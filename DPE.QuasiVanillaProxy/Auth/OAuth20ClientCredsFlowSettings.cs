

namespace DPE.QuasiVanillaProxy.Auth
{
    public class OAuth20ClientCredsFlowSettings
    {
        public string? Authority { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? Scope { get; set; }

        public OAuth20ClientCredsFlowSettings() { }
    }
}
