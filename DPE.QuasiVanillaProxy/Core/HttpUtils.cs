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
        public static HttpRequestMessage CreateHttpRequest(Uri targetUri, HttpMethod httpMethod, Stream contentStream, string mediaType, Encoding? encoding = null)
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
            if (contentStream != null && MustHaveRequestBody(httpMethod))
            {
                if (mediaType == null)
                {
                    throw new ArgumentNullException("mediaType");
                }
                var content = CreateHttpContent(contentStream, mediaType, encoding);
                request.Content = content;
            }

            return request;
        }

        public static HttpContent CreateHttpContent(Stream contentStream, string mediaType, Encoding? encoding = null)
        {
            var content = new StreamContent(contentStream);

            if (IsTextualMediaType(mediaType))
            {
                if (encoding == null)
                {
                    encoding = Encoding.Default;
                }
                using (var memoryStream = new MemoryStream())
                {
                    contentStream.CopyTo(memoryStream);
                    var contentBytes = memoryStream.ToArray();
                    var contentString = encoding.GetString(contentBytes);
                    var encodedContent = encoding.GetBytes(contentString);
                    content = new StreamContent(new MemoryStream(encodedContent));
                }
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
}
