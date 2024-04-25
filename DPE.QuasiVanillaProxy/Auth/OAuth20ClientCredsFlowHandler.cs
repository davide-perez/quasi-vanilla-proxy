using DPE.QuasiVanillaProxy.Security;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace DPE.QuasiVanillaProxy.Auth
{
    public class OAuth20ClientCredsFlowHandler : DelegatingHandler
    {
        private IConfidentialClientApplication? _app;
        private string _authority;
        private string _clientId;
        private string _clientSecret;
        private string _scope;
        private string _tokenCachePath;
        private readonly string _tokenCacheFileName = ".msalcache";
        private MsalCacheHelper? _cacheHelper;


        public OAuth20ClientCredsFlowHandler() { }


        public OAuth20ClientCredsFlowHandler(OAuth20ClientCredsFlowSettings? settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            _authority = settings.Authority ?? string.Empty;
            _clientId = settings.ClientId ?? string.Empty;
            _clientSecret = DataProtectionMgt.DecryptSettingValue(settings.ClientSecret ?? string.Empty);
            _scope = settings.Scope ?? string.Empty;
            _tokenCachePath = AppDomain.CurrentDomain.BaseDirectory;
        }


        public OAuth20ClientCredsFlowHandler(string authority, string clientId, string clientSecret, string scope, string tokenCachePath)
        {
            _authority = authority ?? string.Empty;
            _clientId = clientId ?? string.Empty;
            _clientSecret = clientSecret ?? string.Empty;
            _scope = scope ?? string.Empty;
            _tokenCachePath = tokenCachePath ?? AppDomain.CurrentDomain.BaseDirectory;
        }


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _app = await CreateConfidentialClientApplication();
            AuthenticationResult authResult = await _app.AcquireTokenForClient(scopes: new[] { _scope }).ExecuteAsync();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult.AccessToken);
            return await base.SendAsync(request, cancellationToken);
        }


        private async Task<IConfidentialClientApplication> CreateConfidentialClientApplication()
        {
            if (_cacheHelper == null)
            {
                var storageProperties =
                    new StorageCreationPropertiesBuilder(_tokenCacheFileName, _tokenCachePath)
                    .WithCacheChangedEvent(_clientId, _authority)
                    .Build();

                _cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            }

            _app = ConfidentialClientApplicationBuilder.Create(_clientId)
                .WithClientSecret(_clientSecret)
                .WithAuthority(_authority)
                .Build();

            _cacheHelper?.RegisterCache(_app.AppTokenCache);

            return _app;
        }
    }
}
