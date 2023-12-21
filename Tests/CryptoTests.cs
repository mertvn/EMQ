using System;
using System.Text;
using EMQ.Server;
using NUnit.Framework;

namespace Tests;

public class CryptoTests
{
    [Test]
    public void Test_HashPassword_IsDeterministicForTheSameSalt()
    {
        const string password = "password";

        byte[] saltBytes = Encoding.UTF8.GetBytes("salt");
        byte[] hashBytes = CryptoUtils.HashPassword(password, saltBytes);
        string saltStr = Convert.ToHexString(saltBytes);
        string hashStr = Convert.ToHexString(hashBytes);

        Console.WriteLine(saltStr);
        Console.WriteLine(hashStr);

        Assert.That(saltStr == "73616C74");
        Assert.That(hashStr == "7F14D5A54E861DB9AF13BA35AEB6E82C77BA4AAC64C39D8846B2DF7E7F09A42A");
    }

    [Test]
    public void Test_HashPassword_GeneratesDifferentSaltAndHashEachTime()
    {
        const string password = "password";

        byte[] saltBytes1 = CryptoUtils.GenerateSalt();
        byte[] hashBytes1 = CryptoUtils.HashPassword(password, saltBytes1);
        string saltStr1 = Convert.ToHexString(saltBytes1);
        string hashStr1 = Convert.ToHexString(hashBytes1);

        Console.WriteLine(saltStr1);
        Console.WriteLine(hashStr1);

        byte[] saltBytes2 = CryptoUtils.GenerateSalt();
        byte[] hashBytes2 = CryptoUtils.HashPassword(password, saltBytes2);
        string saltStr2 = Convert.ToHexString(saltBytes2);
        string hashStr2 = Convert.ToHexString(hashBytes2);

        Console.WriteLine(saltStr2);
        Console.WriteLine(hashStr2);

        Assert.That(saltStr1 != saltStr2);
        Assert.That(hashStr1 != hashStr2);
    }

    [Test]
    public void Test_VerifyPassword_true()
    {
        const string password = "password";
        const string salt = "73616C74";
        const string hash = "7F14D5A54E861DB9AF13BA35AEB6E82C77BA4AAC64C39D8846B2DF7E7F09A42A";

        bool isValid = CryptoUtils.VerifyPassword(password, salt, hash);
        Assert.That(isValid);
    }

    [Test]
    public void Test_VerifyPassword_false()
    {
        const string password = "nope";
        const string salt = "73616C74";
        const string hash = "7F14D5A54E861DB9AF13BA35AEB6E82C77BA4AAC64C39D8846B2DF7E7F09A42A";

        bool isValid = CryptoUtils.VerifyPassword(password, salt, hash);
        Assert.That(!isValid);
    }
}
