using System;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Server.Db.Entities.Auth;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using Npgsql;

namespace EMQ.Server.Db;

public class DbManager_Auth
{
    public static async Task<T?> GetEntity_Auth<T>(int id) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.GetAsync<T?>(id);
        }
    }

    public static async Task<long> InsertEntity_Auth<T>(T entity) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            bool success = await connection.InsertAsync(entity);
            return entity.GetIdentityValue();
        }
    }

    public static async Task<bool> UpdateEntity_Auth<T>(T entity) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.UpdateAsync(entity);
        }
    }

    public static async Task<bool> DeleteEntity_Auth<T>(T entity) where T : class
    {
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.DeleteAsync(entity);
        }
    }

    public static async Task<VerificationRegister?> GetVerificationRegister(string username)
    {
        const string sql =
            "SELECT * from verification_register where lower(username) = lower(@username)";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.QuerySingleOrDefaultAsync<VerificationRegister?>(sql, new { username });
        }
    }

    public static async Task<VerificationRegister?> GetVerificationRegister(string username, string token)
    {
        const string sql =
            "SELECT * from verification_register where lower(username) = lower(@username) AND token = @token";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.QuerySingleOrDefaultAsync<VerificationRegister?>(sql, new { username, token });
        }
    }

    public static async Task<int> DeleteExpiredVerificationRows()
    {
        int totalAffectedRows = 0;

        string sqlRegister =
            $"DELETE FROM verification_register where created_at < (select now()) - interval '{AuthStuff.RegisterTokenValidMinutes} minutes'";

        string sqlForgottenPassword =
            $"DELETE FROM verification_forgottenpassword where created_at < (select now()) - interval '{AuthStuff.ResetPasswordTokenValidMinutes} minutes'";

        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            totalAffectedRows += await connection.ExecuteAsync(sqlRegister);
            totalAffectedRows += await connection.ExecuteAsync(sqlForgottenPassword);
        }

        return totalAffectedRows;
    }

    public static async Task<User?> FindUserByEmail(string email)
    {
        const string sql = "SELECT * from users where lower(email) = lower(@email)";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.QuerySingleOrDefaultAsync<User?>(sql, new { email });
        }
    }

    public static async Task<User?> FindUserByUsername(string username)
    {
        const string sql = "SELECT * from users where lower(username) = lower(@username)";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.QuerySingleOrDefaultAsync<User?>(sql, new { username });
        }
    }

    public static async Task<bool> IsUsernameAvailable(string username)
    {
        return await FindUserByUsername(username) == null && await GetVerificationRegister(username) == null;
    }

    public static async Task<Secret?> GetSecret(int userId, Guid token)
    {
        const string sql = "SELECT * from secret where user_id = @userId AND token = @token";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.QuerySingleOrDefaultAsync<Secret?>(sql, new { userId, token });
        }
    }

    public static async Task<Secret?> DeleteSecret(int userId)
    {
        const string sql = "DELETE from secret where user_id = @userId";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.QuerySingleOrDefaultAsync<Secret?>(sql, new { userId });
        }
    }

    public static async Task<VerificationForgottenPassword?> GetVerificationForgottenPassword(int userId, string token)
    {
        const string sql =
            "SELECT * from verification_forgottenpassword where user_id = @userId AND token = @token";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return await connection.QuerySingleOrDefaultAsync<VerificationForgottenPassword?>(sql,
                new { userId, token });
        }
    }
}
