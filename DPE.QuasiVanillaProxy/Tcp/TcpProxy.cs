using DPE.QuasiVanillaProxy.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
        public Encoding? SourceEncoding;
        public Encoding? TargetEncoding;
        public int BufferSize { get; set; } = 1024;
        public ILogger<IProxy> Logger { get; private set; }
        public bool IsRunning { get; private set; }


        public TcpProxy(IHttpClientFactory httpClientFactory, ILogger<IProxy> logger)
        {
            Logger = logger ?? NullLogger<IProxy>.Instance;
            _httpClient = httpClientFactory.CreateClient("proxy");
        }


        public TcpProxy(IHttpClientFactory httpClientFactory, TcpProxySettings settings, ILogger<IProxy> logger)
        {
            Logger = logger ?? NullLogger<IProxy>.Instance;
            IPAddress = IPAddress.Parse(settings.ProxyIPAddress);
            Port = settings.ProxyPort;
            TargetUrl = settings.TargetUrl ?? throw new ArgumentNullException(nameof(TargetUrl));
            FixedContentType = settings.ContentTypeHeader ?? "text/plain";
            SourceEncoding = settings.SourceEncoding;
            TargetEncoding = settings.TargetEncoding;
            BufferSize = settings.StreamBufferSize;
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

            Logger.LogInformation($"Starting Tcp proxy on {IPAddress}:{Port} and forwarding to {TargetUrl}");

            _listener = new TcpListener(IPAddress, Port);
            _listener.Start();
            IsRunning = true;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    Logger.LogDebug($"Connection established with client {client.Client.RemoteEndPoint}");

                    while (client.Connected)
                    {
                        using NetworkStream clientStream = client.GetStream();
                        byte[] readBuffer = new byte[BufferSize];
                        int noOfBytesRead = await clientStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                        if(noOfBytesRead > 0)
                        {
                            HttpResponseMessage response = await ForwardAsync(readBuffer, stoppingToken);
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


        public async Task<HttpResponseMessage> ForwardAsync(byte[] payload, CancellationToken stoppingToken)
        {
            HttpRequestMessage request = null;
            HttpResponseMessage response = null;

            try
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    request = CreateProxyHttpRequest(payload);
                    response = await _httpClient.SendAsync(request);
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

            return response;
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


        private HttpRequestMessage CreateProxyHttpRequest(byte[] data)
        {
            return HttpUtils.CreateHttpRequest(TargetUrl, HttpMethod.Post, data, FixedContentType, Encoding.ASCII, Encoding.UTF8);
        }
    }
}
