using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        public static HttpRequestMessage CreateHttpRequest(Uri? targetUri, HttpMethod httpMethod, byte[] contentData, string mediaType, Encoding? sourceEncoding = null, Encoding? targetEncoding = null, ILogger logger = null)
        {
            if (targetUri == null)
            {
                throw new ArgumentNullException("target");
            }
            if (httpMethod == null)
            {
                throw new ArgumentNullException("httpMethod");
            }
            if (sourceEncoding == null)
            {
                throw new ArgumentNullException("sourceEncoding");
            }
            if (logger == null)
            {
                logger = NullLogger.Instance;
            }
            var request = new HttpRequestMessage(httpMethod, targetUri);
            if (contentData != null && MustHaveRequestBody(httpMethod))
            {
                if (mediaType == null)
                {
                    throw new ArgumentNullException("mediaType");
                }
                var content = CreateHttpContent(contentData, mediaType, sourceEncoding, targetEncoding, logger);
                request.Content = content;
            }

            return request;
        }

        public static HttpContent CreateHttpContent(byte[] contentData, string mediaType, Encoding sourceEncoding, Encoding? targetEncoding, ILogger logger = null)
        {
            if (contentData == null)
            {
                throw new ArgumentNullException(nameof(contentData));
            }
            if (mediaType == null)
            {
                throw new ArgumentNullException(nameof(mediaType));
            }
            if (sourceEncoding == null)
            {
                throw new ArgumentNullException(nameof(sourceEncoding));
            }
            if (logger == null)
            {
                logger = NullLogger.Instance;
            }

            bool isTextualContent = IsTextualMediaType(mediaType);
            HttpContent content;

            if (isTextualContent)
            {
                string contentString;
                if (targetEncoding != null && sourceEncoding != targetEncoding)
                {
                    byte[] targetBytes = Encoding.Convert(sourceEncoding, targetEncoding, contentData);
                    contentString = targetEncoding.GetString(targetBytes);
                    content = new StringContent(contentString, targetEncoding);

                    logger.LogDebug($"Textual content with encoding conversion {sourceEncoding.EncodingName} -> {targetEncoding.EncodingName}:\n{contentString}");
                }
                else
                {
                    contentString = sourceEncoding.GetString(contentData);
                    content = new StringContent(contentString, sourceEncoding);

                    logger.LogDebug($"Textual content:\n{contentString}");
                }
            }
            else
            {
                content = new ByteArrayContent(contentData);

                logger.LogDebug("<binary data>");
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
            // POST, PUT, PATCH usually require a request body.
            // DELETE and OPTIONS can have a request body, but it is not required.
            return ReferenceEquals(method, HttpMethod.Post) ||
                   ReferenceEquals(method, HttpMethod.Put) ||
                   ReferenceEquals(method, HttpMethod.Patch);
        }
    }
}
