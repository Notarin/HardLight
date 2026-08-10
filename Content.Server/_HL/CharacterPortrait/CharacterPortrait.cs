using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Content.Shared.Examine;
using Robust.Shared.Network;

namespace Content.Server._HL.CharacterPortrait;

public sealed class CharacterPortrait
{
    private const long MaxImageSizeBytes = 2 * 1024 * 1024; // 2 MB cap

    // Allowed content types
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
    };

    public static async Task<ImageFetchResult> GetImageDataFromUrl(string url, IHttpClientHolder http)
    {
        // Check if valid url
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new ImageFetchResult(ImageFetchStatus.InvalidUrl, [], url, "Not a valid url");

        try
        {
            var response = await http.Client.GetAsync(uri);
            // Checks network code
            if (!response.IsSuccessStatusCode)
            {
                return new ImageFetchResult(
                    ImageFetchStatus.HttpError,
                    [],
                    url,
                    $"Network problem (HTTP {(int)response.StatusCode} {response.ReasonPhrase})");
            }

            // Must be png, jpg or webp
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || contentType is [] || !AllowedContentTypes.Contains(contentType))
            {
                return new ImageFetchResult(
                    ImageFetchStatus.NotImage,
                    [],
                    url,
                    $"Not a png, jpg or webp image (Unexpected content-type: {contentType ?? "none"})");
            }

            // Max byte size check, 2 MB
            if (response.Content.Headers.ContentLength is { } declaredLength &&
                declaredLength > MaxImageSizeBytes)
            {
                return new ImageFetchResult(
                    ImageFetchStatus.TooLarge,
                    [],
                    url,
                    $"Image size exceeded limit {MaxImageSizeBytes / 1024 / 1024} MB");
            }

            // Check real file size
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var buffer = new MemoryStream();
            var chunk = new byte[8192]; // 8 KB read buffer
            long totalRead = 0;

            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(chunk)) > 0)
            {
                // Count total size
                totalRead += bytesRead;

                // If over the limit stop the check
                if (totalRead > MaxImageSizeBytes)
                {
                    return new ImageFetchResult(
                        ImageFetchStatus.TooLarge,
                        [],
                        url,
                        $"Image size exceeded limit {MaxImageSizeBytes / 1024 / 1024} MB");
                }

                // Write it back to the stream
                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead));
            }

            buffer.Position = 0;

            return new ImageFetchResult(ImageFetchStatus.Success, buffer.ToArray(), url);

        }
        catch (TaskCanceledException) // Communication errors
        {
            return new ImageFetchResult(ImageFetchStatus.Canceled, [], url, "Request timed out or was canceled");
        }
        catch (HttpRequestException ex) // Network errors
        {
            return new ImageFetchResult(ImageFetchStatus.NetworkError, [], url, ex.Message);
        }
    }
}
