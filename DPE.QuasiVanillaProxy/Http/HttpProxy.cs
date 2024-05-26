using DPE.QuasiVanillaProxy.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DPE.QuasiVanillaProxy.Http
{
    public class HttpProxy : IProxy
    {
        private readonly HttpClient _httpClient;
        private HttpListener? _listener;
        HttpListenerContext _context;


        public Uri? Url { get; set; }
        public Uri? TargetUrl { get; set; }
        public Encoding? SourceEncoding;
        public Encoding? TargetEncoding;
        public ILogger<IProxy> Logger { get; private set; }
        public bool IsRunning { get; private set; }


        public HttpProxy(IHttpClientFactory httpClientFactory, ILogger<IProxy> logger)
        {
            _httpClient = httpClientFactory.CreateClient("proxy");
            Logger = logger ?? NullLogger<IProxy>.Instance;
        }


        public HttpProxy(IHttpClientFactory httpClientFactory, HttpProxySettings settings, ILogger<IProxy> logger)
        {
            Url = settings.ProxyUrl ?? throw new ArgumentNullException(nameof(Url));
            TargetUrl = settings.TargetUrl ?? throw new ArgumentNullException(nameof(TargetUrl));
            SourceEncoding = settings.SourceEncoding;
            TargetEncoding = settings.TargetEncoding;
            _httpClient = httpClientFactory.CreateClient("proxy");
            Logger = logger ?? NullLogger<IProxy>.Instance;
        }


        public async Task StartAsync(CancellationToken stoppingToken)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Proxy is already running");
            }
            if (Url == null)
            {
                throw new ArgumentNullException(nameof(Url));
            }
            if (TargetUrl == null)
            {
                throw new ArgumentException(nameof(TargetUrl));
            }

            Logger.LogInformation($"Starting Http proxy on {Url} and forwarding to {TargetUrl}");

            _listener = new HttpListener();
            _listener.Prefixes.Add(Url.ToString());
            _listener.Start();
            IsRunning = true;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    _context = await _listener.GetContextAsync();
                    Logger.LogDebug($"Http message incoming");

                    _ = HandleClientAsync(_context, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug("Proxy stopped due to a cancellation request.");
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995) // The I/O operation has been aborted because of either a thread exit or an application request
            {
                Logger.LogDebug("Proxy stopped due to a cancellation request.");
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
                    response = await _httpClient.SendAsync(request);
                    Logger.LogDebug("Response:\n{@Response}", response);
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


        private async Task HandleClientAsync(HttpListenerContext context, CancellationToken stoppingToken)
        {
            HttpListenerRequest listenerRequest = context.Request;
            byte[] payload;

            using (MemoryStream memoryStream = new MemoryStream())
            {
                await listenerRequest.InputStream.CopyToAsync(memoryStream);
                payload = memoryStream.ToArray();
            }

            HttpResponseMessage? response = await ForwardAsync(payload, stoppingToken);
            _ = SendResponseAsync(_context.Response, response);
        }


        private async Task SendResponseAsync(HttpListenerResponse listenerResponse, HttpResponseMessage response)
        {
            Logger.LogDebug($"Sending response to client");

            if (listenerResponse == null)
            {
                throw new ArgumentNullException(nameof(listenerResponse));
            }
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }
            try
            {
                _context.Response.StatusCode = (int)response.StatusCode;
                _context.Response.StatusDescription = response.ReasonPhrase ?? "";
                _context.Response.Headers.Clear();
                foreach (var header in response.Headers)
                {
                    _context.Response.Headers[header.Key] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    if (header.Key.ToLower() != "content-length")
                    {
                        _context.Response.Headers[header.Key] = string.Join(", ", header.Value);
                    }
                }
                await response.Content.CopyToAsync(_context.Response.OutputStream);
                Logger.LogDebug("Proxied response:\n{@ProxiedResponse}", _context.Response);
                listenerResponse.Close();

                Logger.LogInformation("Response sent to client.");
            }
            finally
            {
                response.Dispose();
            }
        }


        private HttpRequestMessage CreateProxyHttpRequest(byte[] data)
        {
            if(_context == null)
            {
                throw new ArgumentNullException(nameof(_context));
            }
            HttpListenerRequest requestToProxy = _context.Request;
            if(requestToProxy == null)
            {
                throw new ArgumentNullException(nameof(requestToProxy));
            }
            HttpMethod method = new HttpMethod(requestToProxy.HttpMethod);
            string mediaType = "";
            if(requestToProxy.ContentType != null)
            {
                mediaType = requestToProxy.ContentType;
            }

            return HttpUtils.CreateHttpRequest(TargetUrl, method, data, mediaType, SourceEncoding, TargetEncoding, Logger);
        }
    }
}
