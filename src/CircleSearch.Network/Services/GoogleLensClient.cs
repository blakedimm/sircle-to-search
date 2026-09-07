using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CircleSearch.Network.Models;

namespace CircleSearch.Network.Services;

public sealed partial class GoogleLensClient : ILensClient
{
    private const string LensUploadEndpoint = "https://lens.google.com/v3/upload";
    private readonly HttpClient _httpClient;

    public GoogleLensClient(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        }
    }

    public async Task<SearchResult> UploadAndSearchAsync(SearchPayload payload, CancellationToken cancellationToken = default)
    {
        if (payload.ImageBytes == null || payload.ImageBytes.Length == 0)
            return SearchResult.Fail("Empty image payload provided.");

        try
        {
            using var form = new MultipartFormDataContent($"----CircleSearchBoundary{Guid.NewGuid():N}");

            var byteContent = new ByteArrayContent(payload.ImageBytes);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(byteContent, "encoded_image", "capture.png");

            using var response = await _httpClient.PostAsync(LensUploadEndpoint, form, cancellationToken);
            int statusCode = (int)response.StatusCode;

            if (response.Headers.Location != null)
            {
                string redirectUrl = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location.AbsoluteUri
                    : new Uri(new Uri("https://lens.google.com"), response.Headers.Location).AbsoluteUri;

                return SearchResult.Ok(redirectUrl, statusCode);
            }

            if (response.IsSuccessStatusCode)
            {
                string html = await response.Content.ReadAsStringAsync(cancellationToken);
                var match = LensUrlRegex().Match(html);

                if (match.Success)
                {
                    string extractedUrl = match.Value.Replace("\\u0026", "&");
                    if (!extractedUrl.StartsWith("http"))
                    {
                        extractedUrl = "https://lens.google.com" + extractedUrl;
                    }
                    return SearchResult.Ok(extractedUrl, statusCode);
                }

                return SearchResult.Ok(LensUploadEndpoint, statusCode);
            }

            return SearchResult.Fail($"HTTP {statusCode}: {response.ReasonPhrase}", statusCode);
        }
        catch (Exception ex)
        {
            return SearchResult.Fail($"Network error: {ex.Message}");
        }
    }

    [GeneratedRegex(@"https?://lens\.google\.com/search\?p=[a-zA-Z0-9_\-]+")]
    private static partial Regex LensUrlRegex();

    public void Dispose() => _httpClient.Dispose();
}