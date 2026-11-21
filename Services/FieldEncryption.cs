using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrgMind.API.Services;

// Scrambles one value with AES-256-GCM. "context" ties the value to where it belongs (company + question),
// so a scrambled value copied somewhere else cannot be read.
public static class FieldEncryption
{
    public const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static bool IsEncrypted(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static byte[] ParseKey(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException("DataEncryptionKey must be set (32 random bytes, base64).");

        byte[] key;
        try { key = Convert.FromBase64String(base64.Trim()); }
        catch (FormatException) { throw new InvalidOperationException("DataEncryptionKey is not valid base64."); }

        if (key.Length != 32)
            throw new InvalidOperationException("DataEncryptionKey must decode to exactly 32 bytes.");
        return key;
    }

    public static string Encrypt(string plain, byte[] key, string context)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, data, cipher, tag, Encoding.UTF8.GetBytes(context));

        var all = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, all, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, all, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, all, NonceSize + TagSize, cipher.Length);
        return Prefix + Convert.ToBase64String(all);
    }

    // Older values that were never scrambled are returned as they are.
    public static string Decrypt(string stored, byte[] key, string context)
    {
        if (!IsEncrypted(stored)) return stored;

        byte[] all;
        try { all = Convert.FromBase64String(stored.Substring(Prefix.Length)); }
        catch (FormatException) { throw new CryptographicException("The stored value is damaged."); }
        if (all.Length < NonceSize + TagSize) throw new CryptographicException("The stored value is damaged.");

        var nonce = all.AsSpan(0, NonceSize);
        var tag = all.AsSpan(NonceSize, TagSize);
        var cipher = all.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(context));
        return Encoding.UTF8.GetString(plain);
    }
}

// Holds the key for the whole app (registered once at start-up).
public sealed class FieldProtector
{
    private readonly byte[] _key;
    public FieldProtector(byte[] key) => _key = key;
    public string Protect(string plain, string context) => FieldEncryption.Encrypt(plain, _key, context);
    public string Unprotect(string stored, string context) => FieldEncryption.Decrypt(stored, _key, context);
}

// Applies the encryption to a form submission's answers: only questions marked "sensitive".
public static class AnswerProtection
{
    private static string Context(string orgId, string key) => $"{orgId}|{key}";

    public static HashSet<string> SensitiveKeys(string? fieldsJson)
    {
        var keys = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(fieldsJson)) return keys;

        using var doc = JsonDocument.Parse(fieldsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return keys;

        foreach (var field in doc.RootElement.EnumerateArray())
        {
            if (field.TryGetProperty("sensitive", out var s) && s.ValueKind == JsonValueKind.True
                && field.TryGetProperty("key", out var k) && k.GetString() is string key)
                keys.Add(key);
        }
        return keys;
    }

    // Before saving: scramble the sensitive answers. Everything else stays as typed.
    public static string Protect(string? fieldsJson, string answersJson, FieldProtector protector, string orgId)
    {
        var sensitive = SensitiveKeys(fieldsJson);
        if (sensitive.Count == 0) return answersJson;

        var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson) ?? new();
        foreach (var key in sensitive.Where(answers.ContainsKey).ToList())
        {
            if (answers[key].Length > 0)
                answers[key] = protector.Protect(answers[key], Context(orgId, key));
        }
        return JsonSerializer.Serialize(answers);
    }

    // After loading: unscramble. A sensitive answer that cannot be unscrambled is an error (wrong key or damage),
    // never silently shown as garbage.
    public static Dictionary<string, string> ReadAll(string dataJson, string? fieldsJson, FieldProtector protector, string orgId)
    {
        var sensitive = SensitiveKeys(fieldsJson);
        var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(dataJson) ?? new();

        foreach (var key in answers.Keys.ToList())
        {
            if (!FieldEncryption.IsEncrypted(answers[key])) continue;
            try
            {
                answers[key] = protector.Unprotect(answers[key], Context(orgId, key));
            }
            catch (CryptographicException) when (!sensitive.Contains(key))
            {
                // Not a sensitive question and not ours: it is simply what the person typed.
            }
        }
        return answers;
    }
}