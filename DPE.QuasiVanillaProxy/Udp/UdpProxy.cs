using DPE.QuasiVanillaProxy.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace DPE.QuasiVanillaProxy.Udp
{
    public class UdpProxy : IProxy
    {
        private readonly HttpClient _httpClient;
        private UdpClient _client;

        public IPAddress IPAddress { get; set; }
        public int Port { get; set; }
        public Uri? TargetUrl { get; set; }
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        public string FixedContentType { get; set; } = "text/plain";
        public ILogger<IProxy> Logger { get; private set; }
        public bool IsRunning { get; private set; }


        public UdpProxy(IHttpClientFactory httpClientFactory, ILogger<IProxy> logger)
        {
            Logger = logger ?? NullLogger<IProxy>.Instance;
            _httpClient = httpClientFactory.CreateClient("proxy");
        }


        public UdpProxy(IHttpClientFactory httpClientFactory, UdpProxySettings settings, ILogger<IProxy> logger)
        {
            Logger = logger ?? NullLogger<IProxy>.Instance;
            IPAddress = IPAddress.Parse(settings.ProxyIPAddress);
            Port = settings.ProxyPort;
            TargetUrl = settings.TargetUrl ?? throw new ArgumentNullException(nameof(TargetUrl));
            FixedContentType = settings.ContentTypeHeader ?? "text/plain";
            _httpClient = httpClientFactory.CreateClient("proxy");
            _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("QuasiVanillaProxy");
        }


        public async Task StartAsync(CancellationToken stoppingToken)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Proxy is already running");
            }
            if (IPAddress == null)
            {
                throw new ArgumentNullException(nameof(IPAddress));
            }
            if (Port < 1 || Port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(Port));
            }
            if (TargetUrl == null)
            {
                throw new ArgumentException(nameof(TargetUrl));
            }

            Logger.LogInformation($"Starting Udp proxy on {IPAddress}:{Port} and forwarding to {TargetUrl}");

            _client = new UdpClient(new IPEndPoint(IPAddress, Port));
            IsRunning = true;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    UdpReceiveResult result = await _client.ReceiveAsync();
                    Logger.LogDebug($"Connection established");

                    using (MemoryStream buffer = new MemoryStream(result.Buffer))
                    {
                        byte[] payload = buffer.ToArray();
                        HttpResponseMessage response = await ForwardAsync(payload, stoppingToken);
                        if (response != null)
                        {
                            try
                            {
                                Logger.LogDebug("Response:\n{@Response}", response);
                                if (response.IsSuccessStatusCode)
                                    Logger.LogInformation($"Forwarding completed");
                                else
                                    Logger.LogError($"An error occured while forwarding: {response.StatusCode} {response.ReasonPhrase}");
                            }
                            finally
                            {
                                response.Dispose();
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("Udp proxy stopped due to a cancellation request.");
            }
            catch (SocketException ex)
            {
                Logger.LogDebug($"Socket exception: {ex}");
            }
            finally
            {
                if (IsRunning)
                {
                    _client.Dispose();
                    IsRunning = false;
                }
            }
        }


        public async Task<HttpResponseMessage?> ForwardAsync(byte[] payload, CancellationToken stoppingToken)
        {
            try
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    HttpRequestMessage request = CreateProxyHttpRequest(payload);

                    string payloadTxt = "";
                    if (request.Content != null)
                    {
                        payloadTxt = await request.Content.ReadAsStringAsync();
                    }
                    Logger.LogDebug($"Message content: \n\n{payloadTxt}\n");
                    Logger.LogDebug("Client for forwarding:\n{@Request}", _httpClient);

                    HttpResponseMessage response = await _httpClient.SendAsync(request);

                    return response;
                }
            }
            catch (HttpRequestException ex)
            {
                // TODO: additional error handling, retries, ...
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("Forwarding canceled due to a cancellation request");
            }

            return null;
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Proxy is not running");
            }
            _client.Dispose();
            IsRunning = false;

            await Task.CompletedTask;
        }


        private HttpRequestMessage CreateProxyHttpRequest(byte[] payload)
        {
            return null;
            // return HttpUtils.CreateHttpRequest(TargetUrl, HttpMethod.Post, contentStream, FixedContentType);
        }
    }
}
