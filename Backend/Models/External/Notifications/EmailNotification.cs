using System.Text.Json.Serialization;

namespace Backend.Models.External.Notifications
{
    public class EmailNotification
    {
        //{ "sender": "egs-notify@nextechnology.xyz", "recipients": ["test@example.com"], "subject": "Test Subject", 
        //    "body": `<img alt = "Embedded Image" src="cid:pain.png"/>`, 
        //    "attachments": [
        //    {
        //        "attachment_name": "pain.ics",
        //        "attachment_data": calendar,
        //        "attachment_mime": "text/calendar"
        //    }]
        //}
        [JsonPropertyName("sender")]
        public string Sender { get; set; }

        [JsonPropertyName("recipients")]
        public string[] Recipients { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }
        
        [JsonPropertyName("attachments")]
        public EmailAttachment[] Attachments { get; set; }
        
    }

    public class EmailAttachment
    {
        [JsonPropertyName("attachment_data")]
        public string AttachmentData { get; set; }
        
        [JsonPropertyName("attachment_name")]
        public string AttachmentName { get; set; }

        [JsonPropertyName("attachment_mime")]
        public string AttachmentMime { get; set; }
    }
}
