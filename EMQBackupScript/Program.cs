using System.Diagnostics;
using EMQ.Server;

namespace EMQBackupScript;

public static class Program
{
    internal static void Main(string[] args)
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

            File.Delete("output_pg_dump.txt");
            while (!proc.StandardOutput.EndOfStream)
            {
                File.AppendAllText("output_pg_dump.txt", proc.StandardOutput.ReadLine() + "\n");
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
            string message = $"EMQ AUTH DATABASE BACKUP SCRIPT FAILED: {e}";
            Console.WriteLine(message);
            Process.Start("cmd.exe", $"/C msg %username% {message}");
        }
    }
}
