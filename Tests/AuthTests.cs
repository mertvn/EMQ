using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Dapper;
using EMQ.Server;
using EMQ.Server.Business;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using Npgsql;
using NUnit.Framework;

namespace Tests;

public class AuthTests
{
    [SetUp]
    public async Task Setup()
    {
        if (await DbManager.IsUsernameAvailable("test_user"))
        {
            string username = "test_user";
            string email = $"{username}@example.com";
            string newPassword = "averyveryverylongpassword";

            bool isValid = await AuthManager.RegisterStep1SendEmail(username, email);
            Assert.That(isValid); // valid can also mean that the duplicate email was sent

            var verificationRegister = await DbManager.GetVerificationRegister(username);
            if (verificationRegister == null)
            {
                throw new Exception("Failed to GetVerificationRegister");
            }

            int userId = await AuthManager.RegisterStep2SetPassword(username, verificationRegister.token, newPassword);
            Assert.That(userId >= 0);
        }
    }

    [Test]
    public async Task Test_Login_true()
    {
        string username = "test_user";
        string password = "averyveryverylongpassword";

        var user = await AuthManager.Login(username, password);
        Assert.That(user != null);
    }

    [Test]
    public async Task Test_Login_Username_false()
    {
        string username = "test_user";
        string password = "nope";

        var user = await AuthManager.Login(username, password);
        Assert.That(user == null);
    }

    [Test]
    public async Task Test_Login_Email_true()
    {
        string username = "test_user@example.com";
        string password = "averyveryverylongpassword";

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var user = await AuthManager.Login(username, password);
        stopWatch.Stop();

        double elapsedS = Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2);
        Assert.That(user != null);
        Assert.That(elapsedS > 1);
    }

    [Test]
    public async Task Test_Login_Email_false()
    {
        string username = "test_user@example.com";
        string password = "nope";

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var user = await AuthManager.Login(username, password);
        stopWatch.Stop();

        double elapsedS = Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2);
        Assert.That(user == null);
        Assert.That(elapsedS > 1);
    }

    [Test]
    public async Task Test_IsUsernameAvailable_true()
    {
        string user = "";

        bool isValid = await DbManager.IsUsernameAvailable(user);
        Assert.That(isValid);
    }

    [Test]
    public async Task Test_IsUsernameAvailable_false()
    {
        string user = "test_user";

        bool isValid = await DbManager.IsUsernameAvailable(user);
        Assert.That(!isValid);
    }

    [Test]
    public async Task Test_Register_true()
    {
        string username = Guid.NewGuid().ToString()[..16];
        string email = $"{username}@example.com";
        string newPassword = "averyveryverylongpassword";

        bool isValid = await AuthManager.RegisterStep1SendEmail(username, email);
        Assert.That(isValid); // valid can also mean that the duplicate email was sent

        var verificationRegister = await DbManager.GetVerificationRegister(username);
        if (verificationRegister == null)
        {
            throw new Exception("Failed to GetVerificationRegister");
        }

        int userId = await AuthManager.RegisterStep2SetPassword(username, verificationRegister.token, newPassword);
        Assert.That(userId >= 0);
    }

    [Test]
    public async Task Test_RegisterStep2SetPassword_false()
    {
        string username = "777";
        string token = "asdf";
        string newPassword = "averyveryverylongpassword";

        int userId = await AuthManager.RegisterStep2SetPassword(username, token, newPassword);
        Assert.That(userId <= 0);
    }

    [Test]
    public async Task Test_ForgottenPassword_true()
    {
        string username = "test_user";
        string email = $"{username}@example.com";
        string newPassword = "averyveryverylongpassword";

        bool isValid = await AuthManager.ForgottenPasswordStep1SendEmail(email);
        Assert.That(isValid); // valid can also mean that there is no user with that email

        var user = await DbManager.FindUserByUsername(username);
        if (user is null)
        {
            throw new Exception("Failed to FindUserByUsername");
        }

        VerificationForgottenPassword? verificationForgottenPassword;
        const string sql = "SELECT * from verification_forgottenpassword where user_id = @userId";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            verificationForgottenPassword =
                await connection.QuerySingleOrDefaultAsync<VerificationForgottenPassword?>(sql,
                    new { userId = user.id, });
        }

        if (verificationForgottenPassword is null)
        {
            throw new Exception("Failed to GetVerificationForgottenPassword");
        }

        int userId =
            await AuthManager.ForgottenPasswordStep2ResetPassword(user.id, verificationForgottenPassword.token,
                newPassword);
        Assert.That(userId >= 0);
    }
}
