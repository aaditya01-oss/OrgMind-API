using System.Text;
using OrgMind.API.Services;
using Xunit;

namespace OrgMind.Tests;

public class AvatarRulesTests
{
    private static byte[] Pad(byte[] start, int total = 100)
    {
        var data = new byte[total];
        Array.Copy(start, data, start.Length);
        return data;
    }

    [Fact]
    public void A_jpeg_is_accepted()
    {
        Assert.Null(AvatarRules.Check(Pad(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }), out var type));
        Assert.Equal("image/jpeg", type);
    }

    [Fact]
    public void A_png_is_accepted()
    {
        Assert.Null(AvatarRules.Check(Pad(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }), out var type));
        Assert.Equal("image/png", type);
    }

    [Fact]
    public void A_webp_is_accepted()
    {
        var head = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBP");
        Assert.Null(AvatarRules.Check(Pad(head), out var type));
        Assert.Equal("image/webp", type);
    }

    [Fact]
    public void A_text_file_renamed_to_jpg_is_refused()
        => Assert.NotNull(AvatarRules.Check(Encoding.UTF8.GetBytes("this is not a picture"), out _));

    [Fact]
    public void An_svg_is_refused()
        => Assert.NotNull(AvatarRules.Check(Encoding.UTF8.GetBytes("<svg onload=alert(1)></svg>"), out _));

    [Fact]
    public void A_gif_is_refused()
        => Assert.NotNull(AvatarRules.Check(Pad(Encoding.ASCII.GetBytes("GIF89a")), out _));

    [Fact]
    public void Empty_and_oversized_files_are_refused()
    {
        Assert.NotNull(AvatarRules.Check(Array.Empty<byte>(), out _));
        Assert.NotNull(AvatarRules.Check(Pad(new byte[] { 0xFF, 0xD8, 0xFF }, AvatarRules.MaxBytes + 1), out _));
    }

    [Fact]
    public async Task Reading_stops_at_the_limit()
    {
        using var big = new MemoryStream(new byte[1000]);
        Assert.Null(await AvatarRules.ReadLimitedAsync(big, 500));

        using var small = new MemoryStream(new byte[100]);
        Assert.Equal(100, (await AvatarRules.ReadLimitedAsync(small, 500))!.Length);
    }
}