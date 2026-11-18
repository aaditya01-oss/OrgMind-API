using System.Text;

namespace OrgMind.API.Services;

public static class AvatarRules
{
    public const int MaxBytes = 256 * 1024;

    // Returns an error text, or null when the picture is acceptable. The type is decided by the
    // file's own first bytes, never by what the browser says it is.
    public static string? Check(byte[] data, out string contentType)
    {
        contentType = "";
        if (data.Length == 0) return "No picture was received.";
        if (data.Length > MaxBytes) return "The picture is too large.";

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            contentType = "image/jpeg";
            return null;
        }

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(png))
        {
            contentType = "image/png";
            return null;
        }

        if (data.Length >= 12
            && Encoding.ASCII.GetString(data, 0, 4) == "RIFF"
            && Encoding.ASCII.GetString(data, 8, 4) == "WEBP")
        {
            contentType = "image/webp";
            return null;
        }

        return "Please use a JPG, PNG or WebP picture.";
    }

    // Reads the request body but gives up (null) as soon as it goes past the limit.
    public static async Task<byte[]?> ReadLimitedAsync(Stream body, int max = MaxBytes)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > max) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}