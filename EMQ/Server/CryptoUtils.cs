using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EMQ.Server;

public static class CryptoUtils
{
    private const int SaltByteCount = 16;

    private const int IterationCount = 780000;

    public const int HashByteCount = 32;

    private static readonly HashAlgorithmName s_hashAlgorithmName = HashAlgorithmName.SHA256;

    public static byte[] Csprng(int byteCount)
    {
        byte[] bytes = new byte[byteCount];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
            return bytes;
        }
    }

    public static byte[] GenerateSalt()
    {
        return Csprng(SaltByteCount);
    }

    public static byte[] HashPassword(string password, byte[] salt)
    {
        using (Rfc2898DeriveBytes pbkdf2 = new(password, salt, IterationCount, s_hashAlgorithmName))
        {
            return pbkdf2.GetBytes(HashByteCount);
        }
    }

    public static bool VerifyPassword(string password, string salt, string hash)
    {
        byte[] newHash = HashPassword(password, Convert.FromHexString(salt));
        string newHashStr = Convert.ToHexString(newHash);
        return hash == newHashStr;
    }

    public static string Sha256Hash(Stream stream)
    {
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        stream.Position = 0;
        return hash;
    }
}
