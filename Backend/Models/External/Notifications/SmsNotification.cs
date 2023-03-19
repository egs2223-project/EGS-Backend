using System.Text.Json.Serialization;

namespace Backend.Models.External.Notifications
{
    public class SmsNotification
    {
        //{ "send_to": "+351xxxxxxxxx", "msg_body": "Hello" }
        [JsonPropertyName("send_to")]
        public string SendTo { get; set; }

        [JsonPropertyName("msg_body")]
        public string MsgBody { get; set; }
    }
}
