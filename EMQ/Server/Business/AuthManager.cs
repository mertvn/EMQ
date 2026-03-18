using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities.Auth;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using MimeKit;
using Npgsql;

namespace EMQ.Server.Business;

public static class AuthManager
{
    // todo logging
    public static async Task<User?> Login(string usernameOrEmail, string password)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        string salt;
        string hash;
        User? dbUser;

        // this check is only correct as long as we don't allow @ in usernames
        if (usernameOrEmail.Contains('@'))
        {
            dbUser = await DbManager_Auth.FindUserByEmail(usernameOrEmail);
        }
        else
        {
            dbUser = await DbManager_Auth.FindUserByUsername(usernameOrEmail);
            if (dbUser == null)
            {
                // usernames aren't private information, so we don't have to delay here
                return dbUser;
            }
        }

        if (dbUser != null)
        {
            salt = dbUser.salt;
            hash = dbUser.hash;
        }
        else
        {
            // prevent email enumeration
            salt = Convert.ToHexString(CryptoUtils.GenerateSalt());
            hash = Convert.ToHexString(CryptoUtils.Csprng(CryptoUtils.HashByteCount));
        }

        bool isValid = CryptoUtils.VerifyPassword(password, salt, hash);
        if (!isValid)
        {
            dbUser = null;
        }

        int randomDelayMs = await RandomDelay();
        stopWatch.Stop();
        Console.WriteLine(
            $"{isValid} {nameof(Login)} took {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s ({randomDelayMs}ms random delay)");

        return dbUser;
    }

    public static async Task<int> RandomDelay(int iterations = 1)
    {
        int totalRandomDelayMs = 0;
        while (iterations > 0)
        {
            int randomDelayMs = CryptoUtils.Csprng(1).Single(); // 0-255 ms
            await Task.Delay(randomDelayMs);
            totalRandomDelayMs += randomDelayMs;
            iterations -= 1;
        }

        return totalRandomDelayMs;
    }

    // todo important don't refresh while player is in a room
    // This method must only be called by an user-initiated request that
    // ends with them replacing their session token with the new one, like ValidateSession().
    // Otherwise clients may not acquire their newest token, and be logged out unexpectedly.
    // Do not move this down to the database layer.
    public static async Task<Secret> RefreshSecretIfNecessary(Secret secret, string ip)
    {
        // Console.WriteLine(
        //     $"time diff: {(DateTime.UtcNow - secret.last_used_at).TotalSeconds.ToString(CultureInfo.InvariantCulture)}");
        if ((DateTime.UtcNow - secret.last_used_at) > AuthStuff.MaxSessionAge)
        {
            Console.WriteLine($"Refreshing session for user {secret.user_id}");
            var token = Guid.NewGuid();
            secret.token = token;
            secret.last_used_at = DateTime.UtcNow;
            secret.ip_last = ip;

            if (await DbManager_Auth.UpdateEntity_Auth(secret))
            {
                return secret;
            }
            else
            {
                throw new Exception("idk"); // todo?
            }
        }
        else
        {
            return secret;
        }
    }

    public static bool IsValidUsername(string str)
    {
        return RegexPatterns.UsernameRegexCompiled.IsMatch(str);
    }

    public static bool IsValidEmail(string str)
    {
        return RegexPatterns.EmailRegexCompiled.IsMatch(str);
    }

    public static async Task<bool> RegisterStep1SendEmail(string username, string email)
    {
        if (!IsValidUsername(username))
        {
            return false;
        }

        if (!IsValidEmail(email))
        {
            return false;
        }

        if (!await DbManager_Auth.IsUsernameAvailable(username))
        {
            return false;
        }

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        var user = await DbManager_Auth.FindUserByEmail(email);
        if (user is null)
        {
            await SendEmail_Register(username, email);
        }
        else
        {
            await SendEmail_EmailAlreadyExists(username, email);
        }

        int randomDelayMs = await RandomDelay();
        stopWatch.Stop();
        Console.WriteLine(
            $"{true} {nameof(RegisterStep1SendEmail)} took {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s ({randomDelayMs}ms random delay)");

        return true;
    }

    private static async Task SendEmail_Register(string username, string email)
    {
        string toName = username;
        string toAddress = email;
        const string subject = "Registration confirmation";
        const string websiteDomainNoProtocol = Constants.WebsiteDomainNoProtocol;

        var registrationToken = Guid.NewGuid();
        int verificationRegisterId = (int)await DbManager_Auth.InsertEntity_Auth(new VerificationRegister
        {
            username = username, email = email, token = registrationToken.ToString(), created_at = DateTime.UtcNow
        });

        if (verificationRegisterId <= 0)
        {
            throw new Exception("idk"); // todo?
        }

        string registrationLink =
            $"{Constants.WebsiteDomain}/SetPasswordPage?username={username}&token={registrationToken}";

        string bodyTextNewRegistration =
            $@"Hello {username},

Click the link below to complete your registration:
{registrationLink}

Please ignore this email if you have not signed up over at {websiteDomainNoProtocol} recently.

{websiteDomainNoProtocol}
";

        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyTextNewRegistration };

        ServerState.EmailQueue.Enqueue(new EmailQueueItem(message, "Register"));
    }

    private static async Task SendEmail_EmailAlreadyExists(string username, string email)
    {
        string toName = username;
        string toAddress = email;
        const string subject = "Already registered";
        const string websiteDomainNoProtocol = Constants.WebsiteDomainNoProtocol;

        string passwordResetLink = $"{Constants.WebsiteDomain}/ForgottenPasswordPage";

        string bodyTextEmailAlreadyExists =
            $@"Hello {username},

You just tried to sign up over at {websiteDomainNoProtocol}, but there is already an account registered with this email address.

Click the link below to start the password reset process if necessary:
{passwordResetLink}

Please ignore this email if you have not tried to sign up over at {websiteDomainNoProtocol} recently.

{websiteDomainNoProtocol}
";

        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyTextEmailAlreadyExists };

        ServerState.EmailQueue.Enqueue(new EmailQueueItem(message, "EmailAlreadyExists"));
    }

    public static async Task<int> RegisterStep2SetPassword(string username, string token, string newPassword)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        int userId = -1;
        if (newPassword.Length is < AuthStuff.MinPasswordLength or > AuthStuff.MaxPasswordLength)
        {
            return userId;
        }

        // todo? we're not checking for email here
        VerificationRegister? verificationRegister = await DbManager_Auth.GetVerificationRegister(username, token);
        if (verificationRegister is null)
        {
            return userId;
        }

        if (!await DbManager_Auth.DeleteEntity_Auth(verificationRegister))
        {
            return userId;
        }

        if ((DateTime.UtcNow - verificationRegister.created_at) >
            TimeSpan.FromMinutes(AuthStuff.RegisterTokenValidMinutes))
        {
            return userId;
        }

        byte[] saltBytes = CryptoUtils.GenerateSalt();
        byte[] hashBytes = CryptoUtils.HashPassword(newPassword, saltBytes);
        string saltStr = Convert.ToHexString(saltBytes);
        string hashStr = Convert.ToHexString(hashBytes);

        var user = await DbManager_Auth.FindUserByUsername(username);
        if (user is null)
        {
            user = new User
            {
                username = username,
                email = verificationRegister.email,
                roles = UserRoleKind.User,
                created_at = DateTime.UtcNow,
                salt = saltStr,
                hash = hashStr
            };
        }
        else
        {
            throw new Exception("User already exists."); // todo?
        }

        userId = (int)await DbManager_Auth.InsertEntity_Auth(user);
        if (userId <= 0)
        {
            throw new Exception("Failed to insert User."); // todo?
        }

        stopWatch.Stop();
        Console.WriteLine(
            $"{true} {nameof(RegisterStep2SetPassword)} took {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s (no random delay)");

        return userId;
    }

    public static async Task<int> ChangePassword(string username, string currentPassword, string newPassword)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        int userId = -1;
        if (newPassword.Length is < AuthStuff.MinPasswordLength or > AuthStuff.MaxPasswordLength)
        {
            return userId;
        }

        var user = await DbManager_Auth.FindUserByUsername(username);
        if (user is null)
        {
            throw new Exception("User doesn't exist."); // todo?
        }
        else
        {
            userId = user.id;
        }

        bool currentPasswordMatches = CryptoUtils.VerifyPassword(currentPassword, user.salt, user.hash);
        if (!currentPasswordMatches)
        {
            userId = -8; // todo hack
            return userId;
        }

        byte[] saltBytes = CryptoUtils.GenerateSalt();
        byte[] hashBytes = CryptoUtils.HashPassword(newPassword, saltBytes);
        string saltStr = Convert.ToHexString(saltBytes);
        string hashStr = Convert.ToHexString(hashBytes);

        user.salt = saltStr;
        user.hash = hashStr;

        if (!await DbManager_Auth.UpdateEntity_Auth(user))
        {
            throw new Exception("Failed to update User."); // todo?
        }

        stopWatch.Stop();
        Console.WriteLine(
            $"{true} {nameof(ChangePassword)} took {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s (no random delay)");

        return userId;
    }

    public static async Task<Secret> CreateSecret(int userId, string ip)
    {
        // delete previous secret if it exists
        await DbManager_Auth.DeleteSecret(userId);

        Secret secret = new()
        {
            user_id = userId,
            token = Guid.NewGuid(),
            ip_created = ip,
            ip_last = ip,
            created_at = DateTime.UtcNow,
            last_used_at = DateTime.UtcNow,
        };

        int secretId = (int)await DbManager_Auth.InsertEntity_Auth(secret);
        if (secretId <= 0)
        {
            throw new Exception("idk"); // todo?
        }

        Console.WriteLine($"Created new secret for {userId}");
        secret.id = secretId;
        return secret;
    }

    public static async Task<bool> ForgottenPasswordStep1SendEmail(string email)
    {
        if (!IsValidEmail(email))
        {
            return false;
        }

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        var user = await DbManager_Auth.FindUserByEmail(email);
        bool isValid = user is not null;
        if (isValid)
        {
            await SendEmail_ForgottenPassword(user!);
        }

        int randomDelayMs = await RandomDelay(5);
        stopWatch.Stop();
        Console.WriteLine(
            $"{isValid} {nameof(ForgottenPasswordStep1SendEmail)} took {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s ({randomDelayMs}ms random delay)");

        return true;
    }

    private static async Task SendEmail_ForgottenPassword(User user)
    {
        string toName = user.username;
        string toAddress = user.email;
        const string subject = "Reset password";
        const string websiteDomainNoProtocol = Constants.WebsiteDomainNoProtocol;

        // todo? check with user_id before inserting
        var resetToken = Guid.NewGuid();
        int verificationForgottenPasswordId = (int)await DbManager_Auth.InsertEntity_Auth(
            new VerificationForgottenPassword
            {
                user_id = user.id, token = resetToken.ToString(), created_at = DateTime.UtcNow
            });

        if (verificationForgottenPasswordId <= 0)
        {
            throw new Exception("idk"); // todo?
        }

        string resetPasswordLink =
            $"{Constants.WebsiteDomain}/ResetPasswordPage?userId={user.id}&token={resetToken}";

        string bodyTextForgottenPassword =
            $@"Hello {user.username},

Click the link below to reset your password:
{resetPasswordLink}

Please ignore this email if you did not initiate this action over at {websiteDomainNoProtocol} recently.

{websiteDomainNoProtocol}
";

        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyTextForgottenPassword };

        ServerState.EmailQueue.Enqueue(new EmailQueueItem(message, "ForgottenPassword"));
    }

    public static async Task<int> ForgottenPasswordStep2ResetPassword(int userId, string token, string newPassword)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        int ret = -1;
        if (newPassword.Length is < AuthStuff.MinPasswordLength or > AuthStuff.MaxPasswordLength)
        {
            return ret;
        }

        VerificationForgottenPassword? verificationForgottenPassword =
            await DbManager_Auth.GetVerificationForgottenPassword(userId, token);
        if (verificationForgottenPassword is null)
        {
            return ret;
        }

        if (!await DbManager_Auth.DeleteEntity_Auth(verificationForgottenPassword))
        {
            return ret;
        }

        if ((DateTime.UtcNow - verificationForgottenPassword.created_at) >
            TimeSpan.FromMinutes(AuthStuff.ResetPasswordTokenValidMinutes))
        {
            return ret;
        }

        byte[] saltBytes = CryptoUtils.GenerateSalt();
        byte[] hashBytes = CryptoUtils.HashPassword(newPassword, saltBytes);
        string saltStr = Convert.ToHexString(saltBytes);
        string hashStr = Convert.ToHexString(hashBytes);

        var user = await DbManager_Auth.GetEntity_Auth<User>(userId);
        if (user is null)
        {
            throw new Exception("User doesn't exist."); // todo?
        }
        else
        {
            ret = user.id;
        }

        user.salt = saltStr;
        user.hash = hashStr;

        if (!await DbManager_Auth.UpdateEntity_Auth(user))
        {
            throw new Exception("Failed to update User."); // todo?
        }

        stopWatch.Stop();
        Console.WriteLine(
            $"{true} {nameof(ForgottenPasswordStep2ResetPassword)} took {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s (no random delay)");

        return ret;
    }

    public static async Task<bool> UnregisterStep1SendEmail(Session session)
    {
        if (!AuthStuff.HasPermission(session, PermissionKind.Login))
        {
            return false;
        }

        var user = await DbManager_Auth.FindUserByUsername(session.Player.Username);
        bool isValid = user is not null;
        if (isValid)
        {
            await SendEmail_UnregisterVerification(user!);
        }

        return true;
    }

    private static async Task SendEmail_UnregisterVerification(User user)
    {
        string toName = user.username;
        string toAddress = user.email;
        const string subject = "Confirm account deletion";
        const string websiteDomainNoProtocol = Constants.WebsiteDomainNoProtocol;

        var token = Guid.NewGuid();
        int verificationId = (int)await DbManager_Auth.InsertEntity_Auth(
            new VerificationUnregister { user_id = user.id, token = token.ToString(), created_at = DateTime.UtcNow });
        if (verificationId <= 0)
        {
            throw new Exception("idk"); // todo?
        }

        string link = $"{Constants.WebsiteDomain}/UnregisterPage?token={token}&step=1";
        string bodyText =
            $@"Hello {user.username},

Please click the link below to start the account deletion process:
{link}

{websiteDomainNoProtocol}
";

        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyText };

        ServerState.EmailQueue.Enqueue(new EmailQueueItem(message, "UnregisterVerification"));
    }

    public static async Task<int> UnregisterStep2AddToUnregisterQueue(int userId, string token)
    {
        int ret = -1;
        VerificationUnregister? verification = await DbManager_Auth.GetVerificationUnregister(userId, token);
        if (verification is null)
        {
            return ret;
        }

        if (!await DbManager_Auth.DeleteEntity_Auth(verification))
        {
            return ret;
        }

        if ((DateTime.UtcNow - verification.created_at) >
            TimeSpan.FromMinutes(AuthStuff.UnregisterTokenValidMinutes))
        {
            return ret;
        }

        var user = await DbManager_Auth.GetEntity_Auth<User>(userId);
        if (user is null)
        {
            throw new Exception("User doesn't exist.");
        }

        ret = user.id;
        await SendEmail_UnregisterQueued(user);

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connectionAuth.OpenAsync();
        await using var transactionAuth = await connectionAuth.BeginTransactionAsync();
        int rows = await connectionAuth.ExecuteAsync(
            "UPDATE users set exc_perm = array_append(exc_perm, @perm) where id = @uid",
            new { uid = ret, perm = (int)PermissionKind.Login }, transactionAuth);
        bool success = rows == 1;
        if (!success)
        {
            throw new Exception("idk");
        }

        await transactionAuth.CommitAsync();
        return ret;
    }

    private static async Task SendEmail_UnregisterQueued(User user)
    {
        string toName = user.username;
        string toAddress = user.email;
        const string subject = "Account marked for deletion";
        const string websiteDomainNoProtocol = Constants.WebsiteDomainNoProtocol;

        var token = Guid.NewGuid();
        int unregisterId = (int)await DbManager_Auth.InsertEntity_Auth(
            new UnregisterQueue { user_id = user.id, token = token.ToString(), created_at = DateTime.UtcNow });
        if (unregisterId <= 0)
        {
            throw new Exception("idk"); // todo?
        }

        string link = $"{Constants.WebsiteDomain}/UnregisterPage?token={token}&step=2";
        string bodyText =
            $@"Hello {user.username},

Your account has been marked for deletion, and will be deleted in {AuthStuff.UnregisterDays} days. If you change your mind please click the link below to stop the account deletion process:
{link}

{websiteDomainNoProtocol}
";

        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyText };

        ServerState.EmailQueue.Enqueue(new EmailQueueItem(message, "UnregisterQueued"));
    }

    public static async Task<int> UnregisterStep2Point5CancelUnregister(string token)
    {
        int ret = -1;
        // surely it doesn't matter that we don't check for user_id here
        UnregisterQueue? unregisterQueue = await DbManager_Auth.GetUnregisterQueue(token);
        if (unregisterQueue is null)
        {
            return ret;
        }

        var user = await DbManager_Auth.GetEntity_Auth<User>(unregisterQueue.user_id);
        if (user is null)
        {
            throw new Exception("User doesn't exist.");
        }

        if (!await DbManager_Auth.DeleteEntity_Auth(unregisterQueue))
        {
            return ret;
        }

        ret = user.id;
        await SendEmail_UnregisterCanceled(user);

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connectionAuth.OpenAsync();
        await using var transactionAuth = await connectionAuth.BeginTransactionAsync();
        int rows = await connectionAuth.ExecuteAsync(
            "UPDATE users set exc_perm = array_remove(exc_perm, @perm) where id = @uid",
            new { uid = ret, perm = (int)PermissionKind.Login }, transactionAuth);
        bool success = rows == 1;
        if (!success)
        {
            throw new Exception("idk");
        }

        await transactionAuth.CommitAsync();
        return ret;
    }

    private static async Task SendEmail_UnregisterCanceled(User user)
    {
        string toName = user.username;
        string toAddress = user.email;
        const string subject = "Account deletion canceled";
        const string websiteDomainNoProtocol = Constants.WebsiteDomainNoProtocol;
        string bodyText =
            $@"Hello {user.username},

Your account has been unmarked for deletion, and will no longer be deleted.

{websiteDomainNoProtocol}
";

        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyText };

        ServerState.EmailQueue.Enqueue(new EmailQueueItem(message, "UnregisterCanceled"));
    }

    public static async Task UnregisterStep3DeleteAccount(UnregisterQueue unregisterQueue)
    {
        int uid = unregisterQueue.user_id;
        Console.WriteLine($"unregistering account eu{uid}");
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connectionAuth.OpenAsync();
        await using var transactionAuth = await connectionAuth.BeginTransactionAsync();

        var user = await DbManager_Auth.GetEntity_Auth<User>(uid);
        if (user == null)
        {
            throw new Exception($"user is null when unregistering account eu{uid}");
        }

        await connectionAuth.ExecuteAsync(
            "DELETE FROM users_label where user_id = @uid",
            new { uid }, transactionAuth);
        await connectionAuth.ExecuteAsync(
            "DELETE FROM users_label_preset where user_id = @uid",
            new { uid }, transactionAuth);
        await connection.ExecuteAsync(
            "DELETE FROM collection_entity where modified_by = @uid",
            new { uid }, transaction);
        await connection.ExecuteAsync(
            $"DELETE FROM collection where id in (select collection_id from collection_users where user_id = @uid and role = {(int)CollectionUsersRoleKind.Owner})",
            new { uid }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM collection_users where user_id = @uid",
            new { uid }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM music_vote where user_id = @uid",
            new { uid }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM quiz_song_history where user_id = @uid",
            new { uid }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM unique_quiz_plays where user_id = @uid",
            new { uid }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM erodle_users where user_id = @uid",
            new { uid }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM erodle_history where user_id = @uid",
            new { uid }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM user_spaced_repetition where user_id = @uid",
            new { uid }, transaction);

        // Yes, I know that this is not the right way to handle transactions on two separate databases and might cause inconsistencies,
        // but it's fine because the user wants their data gone anyways...
        await transaction.CommitAsync();

        bool successAuth = await connectionAuth.DeleteAsync(user, transactionAuth);
        if (successAuth)
        {
            await transactionAuth.CommitAsync();
            Console.WriteLine($"unregistered account eu{uid}");
            await SendEmail_UnregisterSuccessful(user);
        }
        else
        {
            throw new Exception($"failed to unregister eu{uid}");
        }
    }

    private static async Task SendEmail_UnregisterSuccessful(User user)
    {
        string toName = user.username;
        string toAddress = user.email;
        const string subject = "Account deleted";
        const string websiteDomainNoProtocol = Constants.WebsiteDomainNoProtocol;
        string bodyText =
            $@"Hello {user.username},

Your account has been successfully deleted.

{websiteDomainNoProtocol}
";

        var message = new MimeMessage();
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyText };

        ServerState.EmailQueue.Enqueue(new EmailQueueItem(message, "UnregisterSuccessful"));
    }
}
