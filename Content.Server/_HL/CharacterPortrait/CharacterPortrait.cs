using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Content.Shared.Examine;
using Robust.Shared.Network;

namespace Content.Server._HL.CharacterPortrait;

public sealed class CharacterPortrait
{
    private const long MaxImageSizeBytes = 2 * 1024 * 1024; // 5 MB cap

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
    };

    public static async Task<ImageFetchResult> GetImageDataFromUrl(string url, IHttpClientHolder _http)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new ImageFetchResult(ImageFetchStatus.InvalidUrl, [], url, "Not a valid url");

        try
        {
            var response = await _http.Client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                return new ImageFetchResult(
                    ImageFetchStatus.HttpError,
                    [],
                    url,
                    $"Network problem (HTTP {(int)response.StatusCode} {response.ReasonPhrase})");
            }

            // Must be png or jpg
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || contentType is [] || !AllowedContentTypes.Contains(contentType))
            {
                return new ImageFetchResult(
                    ImageFetchStatus.NotImage,
                    [],
                    url,
                    $"Not a png, jpg or webp image (Unexpected content-type: {contentType ?? "none"})");
            }

            // Max byte size
            if (response.Content.Headers.ContentLength is { } declaredLength &&
                declaredLength > MaxImageSizeBytes)
            {
                return new ImageFetchResult(
                    ImageFetchStatus.TooLarge,
                    [],
                    url,
                    $"Image size exceeded limit {MaxImageSizeBytes / 1024 / 1024} MB");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var buffer = new MemoryStream();
            var chunk = new byte[8192]; // 8 KB read buffer
            long totalRead = 0;

            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(chunk)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > MaxImageSizeBytes)
                {
                    // Bail out immediately — don't keep draining an oversized/malicious response
                    return new ImageFetchResult(
                        ImageFetchStatus.TooLarge,
                        [],
                        url,
                        $"Image size exceeded limit {MaxImageSizeBytes / 1024 / 1024} MB");
                }

                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead));
            }

            buffer.Position = 0;

            return new ImageFetchResult(ImageFetchStatus.Success, buffer.ToArray(), url);

        }
        catch (TaskCanceledException)
        {
            return new ImageFetchResult(ImageFetchStatus.Canceled, [], url, "Request timed out or was canceled");
        }
        catch (HttpRequestException ex)
        {
            return new ImageFetchResult(ImageFetchStatus.NetworkError, [], url, ex.Message);
        }
    }
}
