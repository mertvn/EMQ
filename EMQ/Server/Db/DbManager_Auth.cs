using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Entities.Auth;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Quiz.Entities.Concrete;
using Npgsql;

namespace EMQ.Server.Db;

// todo? extract VNDB and Users
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

    public static async Task<string?> GetActiveUserLabelPresetName(int userId)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        string? presetName =
            await connection.ExecuteScalarAsync<string?>(
                "select name from users_label_preset where user_id = @userId and is_active", new { userId });

        return presetName;
    }

    public static async Task<List<UserLabelPreset>> GetUserLabelPresets(int userId)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var presets =
            (await connection.QueryAsync<UserLabelPreset>(
                "select * from users_label_preset where user_id = @userId", new { userId })).ToList();

        return presets;
    }

    public static async Task<bool> UpsertUserLabelPreset(UserLabelPreset preset)
    {
        try
        {
            await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await connection.ExecuteAsync("update users_label_preset set is_active = false where user_id = @user_id",
                new { preset.user_id }, transaction);

            preset.is_active = true;
            bool upserted = await connection.UpsertAsync(preset, transaction);
            if (!upserted)
            {
                Console.WriteLine($"error upserting UserLabelPreset: {JsonSerializer.Serialize(preset, Utils.Jso)}");
                return false;
            }

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    public static async Task DeleteUserLabelPreset(UserLabelPreset preset)
    {
        const string sqlDelete = "DELETE from users_label_preset where user_id = @user_id AND name = @name";
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connection.ExecuteAsync(sqlDelete, new { user_id = preset.user_id, name = preset.name });
    }

    // todo return null if not found
    public static async Task<PlayerVndbInfo[]> GetUserVndbInfo(int userId, string? presetName)
    {
        if (string.IsNullOrEmpty(presetName))
        {
            return Array.Empty<PlayerVndbInfo>();
        }

        // todo? store actual vndb info and return that instead of this
        const string sql =
            "SELECT distinct vndb_uid, database_kind from users_label where user_id = @userId and preset_name = @presetName";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            var userLabel = (await connection.QueryAsync<UserLabel>(sql, new { userId, presetName })).ToList();
            return userLabel.Select(x => new PlayerVndbInfo()
            {
                VndbId = x.vndb_uid, VndbApiToken = null, Labels = null, DatabaseKind = x.database_kind,
            }).ToArray();
        }
    }

    public static async Task<List<UserLabel>> GetUserLabels(int userId, string vndbUid, string presetName)
    {
        const string sql =
            "SELECT * from users_label where user_id = @userId AND vndb_uid = @vndbUid and preset_name = @presetName";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return (await connection.QueryAsync<UserLabel>(sql,
                new { userId, vndbUid, presetName })).ToList();
        }
    }

    public static async Task<List<UserLabelVn>> GetUserLabelVns(List<long> userLabelIds)
    {
        const string sql = @"SELECT * FROM users_label_vn WHERE users_label_id = ANY(@userLabelIds)";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            return (await connection.QueryAsync<UserLabelVn>(sql, new { userLabelIds })).ToList();
        }
    }

    // we delete and recreate the label and the vns it contains every time because it's mendokusai to diff,
    // and it's probably faster this way anyways
    public static async Task<long> RecreateUserLabel(UserLabel userLabel, Dictionary<string, int> vns)
    {
        const string sqlDelete =
            "DELETE from users_label where user_id = @user_id AND vndb_uid = @vndb_uid AND vndb_label_id = @vndb_label_id and preset_name = @preset_name";

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await connection.ExecuteAsync(sqlDelete,
                new { userLabel.user_id, userLabel.vndb_uid, userLabel.vndb_label_id, userLabel.preset_name },
                transaction);

            await connection.InsertAsync(userLabel, transaction);
            long userLabelId = userLabel.id;
            if (userLabelId <= 0)
            {
                throw new Exception("Failed to insert UserLabel");
            }

            if (vns.Any())
            {
                var userLabelVns = new List<UserLabelVn>();
                foreach ((string vnurl, int vote) in vns)
                {
                    var userLabelVn = new UserLabelVn { users_label_id = userLabelId, vnid = vnurl, vote = vote };
                    userLabelVns.Add(userLabelVn);
                }

                bool success = await connection.InsertListAsync(userLabelVns, transaction);
                if (!success)
                {
                    throw new Exception("Failed to insert userLabelVnRows");
                }
            }

            await transaction.CommitAsync();
            return userLabelId;
        }
    }

    public static async Task DeleteUserLabels(int userId, string presetName)
    {
        const string sqlDelete = "DELETE from users_label where user_id = @userId and preset_name = @presetName";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            await connection.ExecuteAsync(sqlDelete, new { userId, presetName });
        }
    }

    public static async Task<List<ResGetUserQuizSettings>> SelectUserQuizSettings(int userId)
    {
        const string sql = "SELECT name, b64 from users_quiz_settings where user_id = @user_id ORDER BY name";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            var usersQuizSettings = await connection.QueryAsync<ResGetUserQuizSettings>(sql, new { user_id = userId });
            return usersQuizSettings.ToList();
        }
    }

    public static async Task InsertUserQuizSettings(int userId, string name, string b64)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            const string sqlDelete = "DELETE from users_quiz_settings where user_id = @user_id AND name = @name";
            await connection.ExecuteAsync(sqlDelete, new { user_id = userId, name = name }, transaction);

            var usersQuizSettings =
                new UserQuizSettings { user_id = userId, name = name, b64 = b64, created_at = DateTime.UtcNow };
            await connection.InsertAsync(usersQuizSettings, transaction);
            await transaction.CommitAsync();
        }
    }

    public static async Task DeleteUserQuizSettings(int userId, string name)
    {
        const string sqlDelete = "DELETE from users_quiz_settings where user_id = @user_id AND name = @name";
        await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth()))
        {
            await connection.ExecuteAsync(sqlDelete, new { user_id = userId, name = name });
        }
    }

    public static async Task SetAvatar(int userId, Avatar avatar)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int rows = await connection.ExecuteAsync("UPDATE users SET avatar = @avatar, skin = @skin where id = @userId",
            new { userId, avatar = avatar.Character, skin = avatar.Skin });
        if (rows != 1)
        {
            throw new Exception($"Error setting avatar for {userId} to {avatar.Character} {avatar.Skin}");
        }

        await transaction.CommitAsync();
    }

    public static async Task UpsertDonorBenefit(DonorBenefit donorBenefit)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        bool success = await connection.UpsertAsync(donorBenefit, transaction);
        if (!success)
        {
            throw new Exception($"Error upserting donor_benefit for {donorBenefit.user_id}");
        }

        await transaction.CommitAsync();
    }

    public static async Task<DonorBenefit?> GetDonorBenefit(int userId)
    {
        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        return await connection.QueryFirstOrDefaultAsync<DonorBenefit>(
            "select * from donor_benefit where user_id = @userId", new { userId });
    }

    public static async Task<bool> IsRegistrationCodeValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (!Guid.TryParse(code, out _))
        {
            return false;
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        return await connection.ExecuteScalarAsync<bool>("select 1 from registration_code where code = @code",
            new { code });
    }

    public static async Task<bool> DeleteRegistrationCode(string? code)
    {
        if (!await IsRegistrationCodeValid(code))
        {
            return false;
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        return await connection.ExecuteScalarAsync<bool>("delete from registration_code where code = @code",
            new { code });
    }
}
