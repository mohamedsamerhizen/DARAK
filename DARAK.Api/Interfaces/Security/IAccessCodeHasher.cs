namespace DARAK.Api.Interfaces;

public interface IAccessCodeHasher
{
    string Hash(string code);

    bool Verify(string code, string storedHash);

    bool IsHashed(string value);

    string HashLegacyDeterministic(string code);
}
