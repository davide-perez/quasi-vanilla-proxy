using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPE.QuasiVanillaProxy.Core
{
    public class HttpUtils
    {
        public static HttpRequestMessage CreateHttpRequest(Uri targetUri, HttpMethod httpMethod, byte[] contentData, string mediaType, Encoding? sourceEncoding = null, Encoding? targetEncoding = null)
        {
            if (targetUri == null)
            {
                throw new ArgumentNullException("target");
            }
            if (httpMethod == null)
            {
                throw new ArgumentNullException("httpMethod");
            }
            var request = new HttpRequestMessage(httpMethod, targetUri);
            if (contentData != null && MustHaveRequestBody(httpMethod))
            {
                if (mediaType == null)
                {
                    throw new ArgumentNullException("mediaType");
                }
                var content = CreateHttpContent(contentData, mediaType, sourceEncoding, targetEncoding);
                request.Content = content;
            }

            return request;
        }

        public static HttpContent CreateHttpContent(byte[] contentData, string mediaType, Encoding? sourceEncoding, Encoding? targetEncoding)
        {
            HttpContent content;

            if (IsTextualMediaType(mediaType) && sourceEncoding != null && targetEncoding != null && (!ReferenceEquals(sourceEncoding, targetEncoding)))
            {
                byte[] sourceBytes = new byte[contentData.Length];
                contentData.CopyTo(sourceBytes, 0);
                byte[] targetBytes = Encoding.Convert(sourceEncoding, targetEncoding, sourceBytes);
                content = new ByteArrayContent(targetBytes);
            }
            else
            {
                content = new ByteArrayContent(contentData);
            }
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);

            return content;
        }


        public static bool IsTextualMediaType(string mediaType)
        {
            List<string> textualMediaTypes = new List<string> {
                "text/",
                "application/xml",
                "application/json",
                "application/xhtml+xml",
                "application/javascript",
                "application/rss+xml",
                "application/soap+xml",
                "application/svg+xml"
            };

            return textualMediaTypes.Any(type => mediaType.StartsWith(type));
        }


        public static bool MustHaveRequestBody(HttpMethod method)
        {
            return !ReferenceEquals(method, HttpMethod.Get) && !ReferenceEquals(method, HttpMethod.Head) &&
                   !ReferenceEquals(method, HttpMethod.Options) && !ReferenceEquals(method, HttpMethod.Delete);
        }
    }

    /*
     * 
     * public async Task<HttpResponseMessage?> ForwardAsync(Stream inputStream, CancellationToken stoppingToken)
{
    try
    {
        if (!stoppingToken.IsCancellationRequested)
        {
            HttpRequestMessage request = CreateProxyHttpRequest(inputStream);

            // Check if the request is null
            if (request == null)
            {
                // Log or handle the null request
                return null;
            }

            using (MemoryStream requestStream = new MemoryStream())
            {
                // Copy the input stream to the request stream
                await inputStream.CopyToAsync(requestStream, stoppingToken);
                
                // Set the position to the beginning of the request stream
                requestStream.Seek(0, SeekOrigin.Begin);

                // Set the request content to the request stream
                request.Content = new StreamContent(requestStream);

                // Log or process the received data
                Logger.LogDebug($"Received data from client and forwarding...");

                // Forward the request asynchronously
                HttpResponseMessage response = await _httpClient.SendAsync(request, stoppingToken);

                // Return the response
                return response;
            }
        }
    }
    catch (HttpRequestException ex)
    {
        // Log or handle the HttpRequestException
    }
    catch (OperationCanceledException)
    {
        Logger.LogDebug("Forwarding canceled due to a cancellation request");
    }

    return null;
}

     * */
}
