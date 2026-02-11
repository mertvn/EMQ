using System.Diagnostics;
using System.Runtime.InteropServices;
using CommandLine;
using EMQ.Server;
using EMQ.Shared.Core;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace EMQBackupScript;

public static class Program
{
    public class Options
    {
        [Option("env", Required = true, HelpText = "Path to .env file")]
        public string Env { get; set; } = "";

        [Option("out", Required = false, HelpText = "Base output directory")]
        public string Out { get; set; } = "";

        [Option("shsout", Required = false, HelpText = "SHS output directory (also determines if shs export runs)")]
        public string ShsOut { get; set; } = "";

        [Option("auth", Required = false, HelpText = "Export AUTH DB?")]
        public bool Auth { get; set; }

        [Option("song", Required = false, HelpText = "Export SONG DB?")]
        public bool Song { get; set; }

        [Option("botg", Required = false, HelpText = "Export BOTG DB?")]
        public bool Botg { get; set; }

        [Option("private", Required = false, HelpText = "Private dumps?")]
        public bool Private { get; set; }
    }

    internal static void Main(string[] args)
    {
        Options? options = null!;
        Parser.Default.ParseArguments<Options>(args).WithParsed(o => { options = o; });

        DotEnv.Load(options.Env);
        bool isPublicDump = !options.Private;
        bool doAuth = options.Auth;
        bool doSong = options.Song;
        bool doBotg = options.Botg;

        bool hasBaseOut = !string.IsNullOrWhiteSpace(options.Out);
        bool hasShsOut = !string.IsNullOrWhiteSpace(options.ShsOut);
        string baseOut = options.Out;
        string shsOut = options.ShsOut;
        if (!hasBaseOut && !hasShsOut)
        {
            throw new Exception("at least one output directory argument is required");
        }

        bool doSHS = hasShsOut;
        if (!doAuth && !doSong && !doBotg && !doSHS)
        {
            throw new Exception("nothing to do");
        }

        if (doAuth && !isPublicDump)
        {
            try
            {
                Directory.CreateDirectory($"{baseOut}/auth");
                Directory.SetCurrentDirectory($"{baseOut}/auth");
                string envVar = "EMQ_REMOTE_AUTH_DATABASE_URL";

                var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
                Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

                string dumpFileName = $"pgdump_{DateTime.UtcNow:yyyy-MM-dd}_{builder.Database}@{builder.Host}.txt";
                var proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "pg_dump",
                        Arguments =
                            $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"p\" -f {dumpFileName} -d \"{builder.Database}\"",
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("cmd.exe", $"/C msg %username% {message}");
                }
            }
        }

        if (doSong)
        {
            try
            {
                Directory.CreateDirectory($"{baseOut}/song");
                Directory.SetCurrentDirectory($"{baseOut}/song");
                string envVar = "EMQ_REMOTE_SONG_DATABASE_URL";

                var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
                Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

                string prelude = isPublicDump ? "public_" : "";
                string dumpFileName =
                    $"{prelude}pgdump_{DateTime.UtcNow:yyyy-MM-dd}_{builder.Database}@{builder.Host}.txt";
                var proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "pg_dump",
                        Arguments =
                            $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"p\" -f {dumpFileName} -d \"{builder.Database}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };

                var includedTables = new List<string>()
                {
                    "",
                    "artist*",
                    "category",
                    "collection*",
                    "edit_queue",
                    "erodle*",
                    "music*",
                    "quiz",
                    "report",
                    "review_queue",
                    "room",
                    "chars_denorm",
                };

                var excludedTables = new List<string>() { "", "*user*", "quiz_song_history", };
                if (isPublicDump)
                {
                    proc.StartInfo.Arguments += string.Join(" -t ", includedTables);
                    proc.StartInfo.Arguments += string.Join(" -T ", excludedTables);
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("cmd.exe", $"/C msg %username% {message}");
                }
            }
        }

        if (doBotg && !isPublicDump)
        {
            try
            {
                Directory.CreateDirectory($"{baseOut}/botg");
                Directory.SetCurrentDirectory($"{baseOut}/botg");
                string envVar = "EMQ_REMOTE_BOTG_DATABASE_URL";

                var builder = ConnectionHelper.GetConnectionStringBuilderWithEnvVar(envVar);
                Environment.SetEnvironmentVariable("PGPASSWORD", builder.Password);

                string dumpFileName = $"pgdump_{DateTime.UtcNow:yyyy-MM-dd}_{builder.Database}@{builder.Host}.txt";
                var proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "pg_dump",
                        Arguments =
                            $"-U \"{builder.Username}\" -h \"{builder.Host}\" -p \"{builder.Port}\" -F \"p\" -f {dumpFileName} -d \"{builder.Database}\"",
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("cmd.exe", $"/C msg %username% {message}");
                }
            }
        }

        if (doSHS)
        {
            try
            {
                string baseDownloadDir = $"{shsOut}/selfhoststorage";
                Directory.CreateDirectory(baseDownloadDir);
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start("cmd.exe", $"/C msg %username% {message}");
                }
            }
        }
    }

    private static void DownloadDirectory(this SftpClient sftpClient, string sourceRemotePath, string destLocalPath)
    {
        Directory.CreateDirectory(destLocalPath);
        IEnumerable<ISftpFile> files = sftpClient.ListDirectory(sourceRemotePath);
        foreach (ISftpFile file in files)
        {
            if (file.Name is not ("." or ".."))
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
