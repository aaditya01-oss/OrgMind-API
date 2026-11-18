using System.Security.Cryptography;

namespace OrgMind.API.Services;

public static class TemporaryPassword
{
    // No 0 / O / o and no 1 / l / I, so it can be read out loud or typed without mistakes.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    public static string Generate(int length = 12)
    {
        var chars = new char[length];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }
}

public static class PasswordChangeGate
{
    // While a reset password is still in use, only these two requests are allowed.
    public static bool Allows(string? path) =>
        path is not null &&
        (path.Equals("/me", StringComparison.OrdinalIgnoreCase) ||
         path.Equals("/me/password", StringComparison.OrdinalIgnoreCase));
}