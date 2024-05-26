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
        private UdpClient? _udpClient;

        public IPAddress IPAddress { get; set; }
        public int Port { get; set; }
        public Uri? TargetUrl { get; set; }
        public string FixedContentType { get; set; } = "text/plain";
        public Encoding? SourceEncoding { get; set; }
        public Encoding? TargetEncoding { get; set; }
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
            SourceEncoding = settings.SourceEncoding;
            TargetEncoding = settings.TargetEncoding;
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

            Logger.LogInformation($"Starting UDP proxy on {IPAddress}:{Port} and forwarding to {TargetUrl}");

            _udpClient = new UdpClient(new IPEndPoint(IPAddress, Port));
            IsRunning = true;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync().WithCancellation(stoppingToken);
                    Logger.LogInformation($"Received data from {result.RemoteEndPoint}");

                    _ = HandleClientAsync(result, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("UDP proxy stopped due to a cancellation request.");
            }
        }


        private async Task HandleClientAsync(UdpReceiveResult result, CancellationToken stoppingToken)
        {
            try
            {
                HttpResponseMessage? response = await ForwardAsync(result.Buffer, stoppingToken);
                if (response != null)
                {
                    try
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            Logger.LogInformation($"Forwarding completed successfully");
                        }
                        else
                        {
                            Logger.LogError($"An error occurred while forwarding: {response.StatusCode} {response.ReasonPhrase}");
                        }
                    }
                    finally
                    {
                        response.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception in client handling: {ex}");
            }
        }

        public async Task<HttpResponseMessage?> ForwardAsync(byte[] payload, CancellationToken stoppingToken)
        {
            HttpRequestMessage request = null;
            HttpResponseMessage response = null;

            try
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    request = CreateProxyHttpRequest(payload);
                    Logger.LogDebug("Request:\n{@Request}", request);
                    response = await _httpClient.SendAsync(request, stoppingToken);
                    Logger.LogDebug("Response:\n{@Response}", response);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError($"HttpRequestException: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("Forwarding canceled due to a cancellation request");
            }

            return response;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Proxy is not running");
            }
            _udpClient?.Close();
            IsRunning = false;

            await Task.CompletedTask;
        }

        private HttpRequestMessage CreateProxyHttpRequest(byte[] data)
        {
            return HttpUtils.CreateHttpRequest(TargetUrl, HttpMethod.Post, data, FixedContentType, SourceEncoding, TargetEncoding, Logger);
        }
    }
}
