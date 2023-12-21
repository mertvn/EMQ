using System;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace EMQ.Server.Business;

public static class EmailManager
{
    private static SmtpClient SmtpClient { get; } = new();

    private static string Host { get; }

    private static int Port { get; }

    private static string Username { get; }

    private static string Password { get; }

    static EmailManager()
    {
        Host = Environment.GetEnvironmentVariable("EMQ_SMTP_HOST")!;
        Port = int.Parse(Environment.GetEnvironmentVariable("EMQ_SMTP_PORT")!);
        Username = Environment.GetEnvironmentVariable("EMQ_SMTP_USERNAME")!;
        Password = Environment.GetEnvironmentVariable("EMQ_SMTP_PASSWORD")!;
    }

    /// Warning: message.From is overridden
    public static async Task SendEmail(MimeMessage message, string description)
    {
        message.From.Clear();
        message.From.Add(new MailboxAddress(Constants.WebsiteName, Username));

        if (!(message.TextBody != null || message.HtmlBody != null))
        {
            throw new Exception("Can't send email with an empty body.");
        }

        Console.WriteLine($"Sending {description} email to {string.Join(", ", message.To)}");

        await SmtpClient.ConnectAsync(Host, Port, SecureSocketOptions.StartTlsWhenAvailable);
        await SmtpClient.AuthenticateAsync(Username, Password);
        await SmtpClient.SendAsync(message);
        await SmtpClient.DisconnectAsync(true);
    }
}
