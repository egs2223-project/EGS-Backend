using System.Text.Json.Serialization;

namespace Backend.Models.External.Notifications
{
    public class EmailNotification
    {
        //        { "send_to": "test@test.com", "subject": "Test Subject", "text": "Text", "html": "Html", "attachments": [
        //         {
        //           content: "asas".toString("base64"),
        //           filename: "attachment.pdf",
        //           type: "application/pdf",
        //         }
        //       ] }

        [JsonPropertyName("send_to")]
        public string SendTo { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("html")]
        public string Html { get; set; }
        
        [JsonPropertyName("attachments")]
        public EmailAttachment[] Attachments { get; set; }
        
    }

    public class EmailAttachment
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
        
        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}
