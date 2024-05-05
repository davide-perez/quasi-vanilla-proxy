using DPE.QuasiVanillaProxy.Security;
using System.Net;
using System.Text;

namespace DPE.QuasiVanillaProxy.Auth
{
    public class BasicAuthHandler : DelegatingHandler
    {
        private NetworkCredential _credentials { get; set; }


        public BasicAuthHandler() { }

        public BasicAuthHandler(BasicAuthSettings? settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            _credentials = new NetworkCredential(settings.UserName, DataProtectionMgt.DecryptSettingValue(settings.Password));
        }


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(_credentials.UserName + ":" + _credentials.Password)));

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
