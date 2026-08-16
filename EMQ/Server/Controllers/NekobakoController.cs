using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using SkiaSharp;

namespace EMQ.Server.Controllers;

[CustomAuthorize(PermissionKind.User)]
[ApiController]
[Route("[controller]")]
public sealed class NekobakoController : ControllerBase
{
    private const int MaxDimension = 8192;
    private const long MaxPixelCount = 24_000_000;
    private const int WebpQuality = 90;

    [CustomAuthorize(PermissionKind.NekobakoUpload)]
    [EnableRateLimiting(RateLimitKind.UploadFile)]
    [RequestSizeLimit(UploadConstants.NekobakoMaxFilesizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadConstants.NekobakoMaxFilesizeBytes)]
    [HttpPost]
    [Route("UploadFile")]
    public async Task<ActionResult<string>> UploadFile([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (ServerState.Config.IsServerReadOnly || ServerState.Config.IsSubmissionDisabled)
        {
            return Unauthorized();
        }

        Session? session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        switch (file.Length)
        {
            case 0:
                return BadRequest("The uploaded file is empty.");
            case > UploadConstants.NekobakoMaxFilesizeBytes:
                return StatusCode(StatusCodes.Status413PayloadTooLarge,
                    "The uploaded file size is too large.");
        }

        string originalFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return BadRequest("The uploaded file does not have a filename.");
        }

        var id = Guid.NewGuid();
        string extension = Path.GetExtension(file.FileName);
        if (extension.Length > 16)
        {
            extension = "";
        }

        var fs = file.OpenReadStream();
        string tempFilePath = $"{Path.GetTempPath()}{Guid.NewGuid().ToString()}";
        try
        {
            using var codec = SKCodec.Create(fs);
            bool isImageFile = codec != null;
            if (isImageFile)
            {
                if (codec!.EncodedFormat is not (SKEncodedImageFormat.Bmp or SKEncodedImageFormat.Png
                    or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
                {
                    return BadRequest("The uploaded file is not a supported image.");
                }

                var sourceInfo = codec.Info;
                if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0)
                {
                    return BadRequest("The image has invalid dimensions.");
                }

                if (sourceInfo.Width > MaxDimension || sourceInfo.Height > MaxDimension)
                {
                    return BadRequest(
                        $"Image dimensions may not exceed {MaxDimension} × {MaxDimension} pixels.");
                }

                long pixelCount = (long)sourceInfo.Width * sourceInfo.Height;
                if (pixelCount > MaxPixelCount)
                {
                    return BadRequest(new
                    {
                        error = $"The image contains too many pixels. Maximum: {MaxPixelCount:N0}."
                    });
                }

                var decodeInfo = new SKImageInfo(sourceInfo.Width, sourceInfo.Height,
                    SKColorType.Rgba8888, SKAlphaType.Premul);
                using var decoded = SKBitmap.Decode(codec, decodeInfo);
                if (decoded == null)
                {
                    return BadRequest("The image could not be decoded.");
                }

                using var image = SKImage.FromBitmap(decoded);
                using var webp = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
                if (webp == null || webp.Size == 0)
                {
                    return BadRequest("The image could not be converted to WebP.");
                }

                fs = new FileStream(tempFilePath, FileMode.Create);
                webp.SaveTo(fs);
                fs.Position = 0;
                extension = ".webp";
            }
            else
            {
                using var reader = new StreamReader(file.OpenReadStream(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));

                string text;
                try
                {
                    text = await reader.ReadToEndAsync(cancellationToken);
                }
                catch (Exception)
                {
                    return BadRequest("The file is neither a supported image nor valid UTF-8 text.");
                }

                fs = new FileStream(tempFilePath, FileMode.Create);
                await new MemoryStream(Encoding.UTF8.GetBytes(text)).CopyToAsync(fs, cancellationToken);
                extension = ".txt";
            }

            string storedFileName = $"{id}{extension}";
            fs.Position = 0;
            string sha256 = CryptoUtils.Sha256Hash(fs);
            await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());

            var existingFile = await connectionAuth.QuerySingleOrDefaultAsync<(Guid id, string extension)>(
                "select id, extension from users_nekobako where user_id = @uid and sha256 = @sha256",
                new { uid = session.Player.Id, sha256 });
            if (existingFile.id != default)
            {
                storedFileName = $"{existingFile.id}{existingFile.extension}";
            }
            else
            {
                Console.WriteLine($"eu{session.Player.Id} UploadFile {file.Length} bytes, {storedFileName}");
                await connectionAuth.OpenAsync(cancellationToken);
                await using var transactionAuth = await connectionAuth.BeginTransactionAsync(cancellationToken);
                if (!await connectionAuth.InsertAsync(new UserNekobako
                    {
                        id = id,
                        extension = extension,
                        user_id = session.Player.Id,
                        size_bytes = fs.Length,
                        sha256 = sha256,
                        orig_name = originalFileName,
                        uploaded_at = DateTime.UtcNow,
                    }, transactionAuth))
                {
                    return StatusCode(500, "Failed to insert the file into the database.");
                }

                // todo this sometimes fails without reporting any errors
                // todo split files into multiple dirs
                ServerUtils.SftpFileUpload(
                    UploadConstants.SftpHost, UploadConstants.SftpUsername,
                    UploadConstants.SftpPassword,
                    fs, $"{UploadConstants.SftpUserUploadDir}nekobako/{storedFileName}");
                await transactionAuth.CommitAsync(cancellationToken);
            }

            return Utils.FormatNekobakoLink(storedFileName);
        }
        finally
        {
            if (fs != null!)
            {
                await fs.DisposeAsync();
            }

            if (tempFilePath != null! && System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }
        }
    }

    [CustomAuthorize(PermissionKind.User)]
    [HttpPost]
    [Route("ListUserFiles")]
    public async Task<ActionResult<string>> ListUserFiles()
    {
        Session? session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var userNekobakos = (await connectionAuth.QueryAsync<UserNekobako>(
            "select * from users_nekobako where user_id = @uid", new { uid = session.Player.Id })).ToList();
        foreach (UserNekobako userNekobako in userNekobakos)
        {
            userNekobako.user_id = default;
            userNekobako.sha256 = default!;
        }

        return JsonSerializer.Serialize(userNekobakos, Utils.JsoCompactAggressive);
    }

    [CustomAuthorize(PermissionKind.UseUploadedImageAvatar)]
    [HttpPost]
    [Route("ListAvatarImages")]
    public async Task<ActionResult<UserNekobako[]>> ListAvatarImages()
    {
        Session? session = AuthStuff.GetSession(HttpContext.Items);
        if (session == null)
        {
            return Unauthorized();
        }

        const string sql = @"SELECT * FROM users_nekobako
WHERE user_id = @userId AND extension = '.webp' AND size_bytes <= @maxSizeBytes
ORDER BY uploaded_at DESC";

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var images = (await connectionAuth.QueryAsync<UserNekobako>(sql,
            new { userId = session.Player.Id, maxSizeBytes = UploadConstants.AvatarMaxFilesizeBytes })).ToArray();
        foreach (UserNekobako image in images)
        {
            image.user_id = default;
            image.sha256 = default!;
        }

        return images;
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("ListAllFiles")]
    public async Task<ActionResult<UserNekobako[]>> ListAllFiles()
    {
        const string sql = @"SELECT * FROM users_nekobako ORDER BY uploaded_at DESC";
        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        var files = (await connectionAuth.QueryAsync<UserNekobako>(sql)).ToArray();
        return files;
    }
}
