using DPE.QuasiVanillaProxy.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace DPE.QuasiVanillaProxy.Http
{
    public class HttpProxy : IProxy
    {
        private readonly HttpClient _httpClient;
        private HttpListener? _listener;

        public Uri? Url { get; set; }
        public Uri? TargetUrl { get; set; }
        public ILogger<IProxy> Logger { get; private set; }
        public bool IsRunning { get; private set; }
        

        public HttpProxy(IHttpClientFactory httpClientFactory, ILogger<IProxy> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            Logger = logger ?? NullLogger<IProxy>.Instance;
        }


        public HttpProxy(IHttpClientFactory httpClientFactory, HttpProxySettings settings, ILogger<IProxy> logger)
        {
            Url = settings.ProxyUrl ?? throw new ArgumentNullException(nameof(Url));
            TargetUrl = settings.TargetUrl ?? throw new ArgumentNullException(nameof(TargetUrl));
            _httpClient = httpClientFactory.CreateClient();
            Logger = logger ?? NullLogger<IProxy>.Instance;
        }


        public async Task StartAsync(CancellationToken stoppingToken)
        {
            if(IsRunning) {
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
                    HttpListenerContext context = await _listener.GetContextAsync();
                    Logger.LogDebug($"Http message incoming");

                    using (Stream inputStream = context.Request.InputStream)
                    {
                        HttpResponseMessage response = await ForwardAsync(inputStream, stoppingToken);
                        if (response != null)
                        {
                            try
                            {
                                Logger.LogDebug("Response:\n{@Response}", response);
                                context.Response.StatusCode = (int) response.StatusCode;
                                context.Response.StatusDescription = response.ReasonPhrase ?? "";
                                context.Response.Headers.Clear();
                                foreach (var header in response.Headers)
                                {
                                    context.Response.Headers[header.Key] = string.Join(", ", header.Value);
                                }
                                foreach (var header in response.Content.Headers)
                                {
                                    if (header.Key.ToLower() != "content-length")
                                    {
                                        context.Response.Headers[header.Key] = string.Join(", ", header.Value);
                                    }
                                }
                                await response.Content.CopyToAsync(context.Response.OutputStream);
                                if (response.IsSuccessStatusCode)
                                    Logger.LogInformation($"Forwarding completed");
                                else
                                    Logger.LogError($"An error occured while forwarding: {context.Response.StatusCode} {context.Response.StatusDescription}");
                            }
                            finally
                            {
                                response.Dispose();
                            }
                        }
                    }
                    context.Response.Close();
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
            finally
            {
                IsRunning = false;
            }
        }


        public async Task<HttpResponseMessage> ForwardAsync(Stream inputStream, CancellationToken stoppingToken)
        {
            try
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    using StreamReader reader = new StreamReader(inputStream, Encoding.UTF8);
                    string msgToForward = await reader.ReadToEndAsync();
                    Logger.LogDebug($"Message content: \n\n{msgToForward}\n");
                    using var content = new StringContent(msgToForward, Encoding.UTF8, "application/json"); // TODO: handle multi-type content (binary, ...)
                    Logger.LogDebug("Client for forwarding:\n{@Request}", _httpClient);
                    HttpResponseMessage response = await _httpClient.PostAsync(TargetUrl, content);

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
    }
}
