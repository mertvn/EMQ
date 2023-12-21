using MimeKit;

namespace EMQ.Server;

public class EmailQueueItem
{
    public EmailQueueItem(MimeMessage mimeMessage, string description)
    {
        MimeMessage = mimeMessage;
        Description = description;
    }

    public MimeMessage MimeMessage { get; set; }

    /// for logging purposes
    public string Description { get; set; }
}
