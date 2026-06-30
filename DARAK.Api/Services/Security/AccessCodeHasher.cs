using System.Security.Cryptography;
using System.Text;
using DARAK.Api.Interfaces;

namespace DARAK.Api.Services;

public sealed class AccessCodeHasher : IAccessCodeHasher
{
    private const string CurrentPrefix = "AC2";
    private const string LegacySha256Prefix = "SHA256HEX$";
    private const int SaltSize = 16;

    public string Hash(string code)
    {
        var normalizedCode = Normalize(code);
        if (normalizedCode is null)
        {
            throw new ArgumentException("Access code is required.", nameof(code));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(salt, normalizedCode);
        return $"{CurrentPrefix}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string code, string storedHash)
    {
        var normalizedCode = Normalize(code);
        var trimmedStoredHash = TrimOrNull(storedHash);
        if (normalizedCode is null || trimmedStoredHash is null)
        {
            return false;
        }

        if (trimmedStoredHash.StartsWith($"{CurrentPrefix}$", StringComparison.Ordinal))
        {
            return VerifyCurrentHash(normalizedCode, trimmedStoredHash);
        }

        if (trimmedStoredHash.StartsWith(LegacySha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var expected = HashLegacyDeterministic(normalizedCode);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(trimmedStoredHash.ToUpperInvariant()));
        }

        return string.Equals(normalizedCode, trimmedStoredHash, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsHashed(string value)
    {
        var trimmedValue = TrimOrNull(value);
        return trimmedValue is not null
            && (trimmedValue.StartsWith($"{CurrentPrefix}$", StringComparison.Ordinal)
                || trimmedValue.StartsWith(LegacySha256Prefix, StringComparison.OrdinalIgnoreCase));
    }

    public string HashLegacyDeterministic(string code)
    {
        var normalizedCode = Normalize(code);
        if (normalizedCode is null)
        {
            throw new ArgumentException("Access code is required.", nameof(code));
        }

        var bytes = Encoding.Unicode.GetBytes(normalizedCode);
        return LegacySha256Prefix + Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static bool VerifyCurrentHash(string normalizedCode, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 3 || parts[0] != CurrentPrefix)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expectedHash = Convert.FromBase64String(parts[2]);
            var actualHash = ComputeHash(salt, normalizedCode);
            return expectedHash.Length == actualHash.Length
                && CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] ComputeHash(byte[] salt, string normalizedCode)
    {
        using var hmac = new HMACSHA256(salt);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedCode));
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
