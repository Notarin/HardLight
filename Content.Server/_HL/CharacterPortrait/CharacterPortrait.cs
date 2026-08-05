using System.IO;
using System.Threading.Tasks;
using Robust.Shared.Network;

namespace Content.Server._HL.CharacterPortrait;

public sealed class CharacterPortrait
{
    private const long MaxImageSizeBytes = 2 * 1024 * 1024; // 5 MB cap

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp", // MAYBE?
    };

    public static async Task<byte[]?> GetImageDataFromUrl(string url, IHttpClientHolder _http)
    {
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var response = await _http.Client.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        // Must be png or jpg
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !AllowedContentTypes.Contains(contentType))
            return null;

        // Max byte size
        if (response.Content.Headers.ContentLength is { } declaredLength &&
            declaredLength > MaxImageSizeBytes)
        {
            return null;
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
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead));
        }

        buffer.Position = 0;

        return buffer.ToArray();
    }
}
