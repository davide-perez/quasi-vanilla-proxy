using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using System.Text;
using DPE.QuasiVanillaProxy.Core;

namespace DPE.QuasiVanillaProxy.Tcp
{
    public class TcpProxy : IProxy
    {
        private readonly HttpClient _httpClient;
        private TcpListener? _listener;

        public IPAddress IPAddress { get; set; }
        public int Port { get; set; }
        public Uri? TargetUrl { get; set; }
        public string FixedContentType { get; set; } = "text/plain";
        public ILogger<IProxy> Logger { get; private set; }
        public bool IsRunning { get; private set; }


        public TcpProxy(IHttpClientFactory httpClientFactory, ILogger<IProxy> logger)
        {
            Logger = logger ?? NullLogger<IProxy>.Instance;
            _httpClient = httpClientFactory.CreateClient();
        }


        public TcpProxy(IHttpClientFactory httpClientFactory, TcpProxySettings settings, ILogger<IProxy> logger)
        {
            Logger = logger ?? NullLogger<IProxy>.Instance;
            IPAddress = IPAddress.Parse(settings.ProxyIPAddress);
            Port = settings.ProxyPort;
            TargetUrl = settings.TargetUrl ?? throw new ArgumentNullException(nameof(TargetUrl));
            FixedContentType = settings.ContentTypeHeader ?? "text/plain";
            _httpClient = httpClientFactory.CreateClient();
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
            if(Port < 1 || Port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(Port));
            }
            if (TargetUrl == null)
            {
                throw new ArgumentException(nameof(TargetUrl));
            }

            Logger.LogInformation($"Starting Tcp proxy on {IPAddress}:{Port} and forwarding to {TargetUrl}");

            _listener = new TcpListener(IPAddress, Port);
            _listener.Start();
            IsRunning = true;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    Logger.LogDebug($"Tcp message incoming");


                    using (Stream inputStream = client.GetStream())
                    {
                        HttpResponseMessage response = await ForwardAsync(inputStream, stoppingToken);
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
                Logger.LogDebug("Tcp proxy stopped due to a cancellation request.");
            }
            catch (SocketException ex)
            {
                Logger.LogDebug($"Socket exception: {ex}");
            }
            finally
            {
                if (IsRunning)
                {
                    _listener.Stop();
                    IsRunning = false;
                }
            }
        }


        public async Task<HttpResponseMessage> ForwardAsync(Stream inputStream, CancellationToken stoppingToken)
        {
            try
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    HttpRequestMessage request = CreateProxyHttpRequest(inputStream);

                    string payloadTxt = "";
                    if(request.Content != null)
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
            _listener?.Stop();
            IsRunning = false;

            await Task.CompletedTask;
        }


        private HttpRequestMessage CreateProxyHttpRequest(Stream contentStream)
        {
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, TargetUrl);
            requestMsg.Content = new StreamContent(contentStream);
            requestMsg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(FixedContentType);

            return requestMsg;
        }
    }
}
