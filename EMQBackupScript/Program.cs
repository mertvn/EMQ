using System.Diagnostics;
using EMQ.Server;
using EMQ.Shared.Core;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace EMQBackupScript;

public static class Program
{
    internal static void Main(string[] args)
    {
        DotEnv.Load(@"C:/emq/.env");
        bool doAuth = false;
        bool doSong = true;
        bool doBotg = false;
        bool doSHS = false;

        bool isPublicDump = true;

        if (doAuth)
        {
            try
            {
                Directory.SetCurrentDirectory(@"C:/emq/dbbackups/auth");
                string envVar = "EMQ_REMOTE_AUTH_DATABASE_URL";

                var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
                Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

                string dumpFileName = $"pgdump_{DateTime.UtcNow:yyyy-MM-dd}_{builder.Database}@{builder.Host}.tar";
                var proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "pg_dump",
                        Arguments =
                            $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"t\" -f {dumpFileName} -d \"{builder.Database}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    }
                };
                proc.Start();

                File.Delete("output_pg_dump_auth.txt");
                while (!proc.StandardOutput.EndOfStream)
                {
                    File.AppendAllText("output_pg_dump_auth.txt", proc.StandardOutput.ReadLine() + "\n");
                }

                if (!File.Exists(dumpFileName))
                {
                    throw new Exception("pg_dump failed");
                }

                long filesizeBytes = new FileInfo(dumpFileName).Length;
                if (filesizeBytes <= (70 * 1000)) // 70 KB
                {
                    throw new Exception("filesizeBytes is too small");
                }
            }
            catch (Exception e)
            {
                string message = $"EMQ AUTH DATABASE BACKUP SCRIPT FAILED: {e}";
                Console.WriteLine(message);
                Process.Start("cmd.exe", $"/C msg %username% {message}");
            }
        }

        if (doSong)
        {
            try
            {
                Directory.CreateDirectory(@"C:/emq/dbbackups/song");
                Directory.SetCurrentDirectory(@"C:/emq/dbbackups/song");
                string envVar = "EMQ_REMOTE_SONG_DATABASE_URL";

                var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
                Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

                string prelude = isPublicDump ? "public_" : "";
                string dumpFileName = $"{prelude}pgdump_{DateTime.UtcNow:yyyy-MM-dd}_{builder.Database}@{builder.Host}.tar";
                var proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "pg_dump",
                        Arguments =
                            $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"t\" -f {dumpFileName} -d \"{builder.Database}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };

                if (isPublicDump)
                {
                    proc.StartInfo.Arguments +=
                        " -t \"artist*\" -t category -t edit_queue -t \"erodle*\" -t \"music*\" -t quiz -t report -t review_queue -t room -T \"*user*\" -T quiz_song_history";
                }

                proc.Start();

                File.Delete("output_pg_dump_song.txt");
                while (!proc.StandardOutput.EndOfStream)
                {
                    File.AppendAllText("output_pg_dump_song.txt", proc.StandardOutput.ReadLine() + "\n");
                }

                if (!File.Exists(dumpFileName))
                {
                    File.AppendAllText("err_pg_dump_song.txt", proc.StandardError.ReadLine() + "\n");
                    throw new Exception("pg_dump failed");
                }

                long filesizeBytes = new FileInfo(dumpFileName).Length;
                if (filesizeBytes <= (7000 * 1000)) // 7000 KB
                {
                    throw new Exception("filesizeBytes is too small");
                }

                var procZstd = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "zstd",
                        Arguments = $"-19 --rm -f {dumpFileName}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    }
                };
                procZstd.Start();

                File.Delete("output_zstd_song.txt");
                while (!procZstd.StandardOutput.EndOfStream)
                {
                    File.AppendAllText("output_zstd_song.txt", procZstd.StandardOutput.ReadLine() + "\n");
                }
            }
            catch (Exception e)
            {
                string message = $"EMQ SONG DATABASE BACKUP SCRIPT FAILED: {e}";
                Console.WriteLine(message);
                Process.Start("cmd.exe", $"/C msg %username% {message}");
            }
        }

        if (doBotg)
        {
            try
            {
                Directory.SetCurrentDirectory(@"C:/emq/dbbackups/botg");
                string envVar = "EMQ_REMOTE_BOTG_DATABASE_URL";

                var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
                Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

                string dumpFileName = $"pgdump_{DateTime.UtcNow:yyyy-MM-dd}_{builder.Database}@{builder.Host}.tar";
                var proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "pg_dump",
                        Arguments =
                            $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"t\" -f {dumpFileName} -d \"{builder.Database}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    }
                };
                proc.Start();

                File.Delete("output_pg_dump_botg.txt");
                while (!proc.StandardOutput.EndOfStream)
                {
                    File.AppendAllText("output_pg_dump_botg.txt", proc.StandardOutput.ReadLine() + "\n");
                }

                if (!File.Exists(dumpFileName))
                {
                    throw new Exception("pg_dump failed");
                }

                long filesizeBytes = new FileInfo(dumpFileName).Length;
                if (filesizeBytes <= (7 * 1000)) // 7 KB
                {
                    throw new Exception("filesizeBytes is too small");
                }
            }
            catch (Exception e)
            {
                string message = $"EMQ BOTG DATABASE BACKUP SCRIPT FAILED: {e}";
                Console.WriteLine(message);
                Process.Start("cmd.exe", $"/C msg %username% {message}");
            }
        }

        if (doSHS)
        {
            try
            {
                const string baseDownloadDir = "K:\\emq\\emqsongsbackup2\\selfhoststorage";
                var connectionInfo =
                    new Renci.SshNet.ConnectionInfo(UploadConstants.SftpHost, UploadConstants.SftpUsername,
                        new PasswordAuthenticationMethod(UploadConstants.SftpUsername, UploadConstants.SftpPassword));
                using (var client = new SftpClient(connectionInfo))
                {
                    client.Connect();
                    string replaced = UploadConstants.SftpUserUploadDir.Replace("/userup", "");
                    client.DownloadDirectory(replaced, baseDownloadDir);
                    client.Disconnect();
                }
            }
            catch (Exception e)
            {
                string message = $"EMQ SELFHOSTSTORAGE BACKUP SCRIPT FAILED: {e}";
                Console.WriteLine(message);
                Process.Start("cmd.exe", $"/C msg %username% {message}");
            }
        }
    }

    private static void DownloadDirectory(this SftpClient sftpClient, string sourceRemotePath, string destLocalPath)
    {
        Directory.CreateDirectory(destLocalPath);
        IEnumerable<ISftpFile> files = sftpClient.ListDirectory(sourceRemotePath);
        foreach (ISftpFile file in files)
        {
            if (file.Name is not ("." or ".." or "vndb-img"))
            {
                string sourceFilePath = $"{sourceRemotePath}/{file.Name}";
                string destFilePath = Path.Combine(destLocalPath, file.Name);
                if (file.IsDirectory)
                {
                    DownloadDirectory(sftpClient, sourceFilePath, destFilePath);
                }
                else
                {
                    if (!File.Exists(destFilePath))
                    {
                        string tempPath = Path.GetTempFileName();
                        using Stream fileStream = new FileStream(tempPath, FileMode.Create);
                        sftpClient.DownloadFile(sourceFilePath, fileStream);
                        fileStream.Dispose();
                        File.Move(tempPath, destFilePath);
                    }
                }
            }
        }
    }
}
