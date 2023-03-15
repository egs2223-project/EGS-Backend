using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Backend.Models
{
    public class Patient : User
    {
        [Required]
        [JsonPropertyName("pacient_code")]
        public string PacientCode { get; set; }

        [Required]
        [JsonPropertyName("notification_preferences")]
        public NotificationPreferences Preferences { get; set; }

        //public Pacient() : base()
        //{
        //    PacientCode = "sasda";
        //    Preferences = new NotificationPreferences();
        //}

        public class NotificationPreferences
        {
            [Key]
            [JsonIgnore]
            public long Id { get; set; }
            [JsonPropertyName("email")]
            public bool Email { get; set; }
            [JsonPropertyName("sms")]
            public bool Sms { get; set; }

            //public NotificationPreferences()
            //{
            //    Email = true;
            //    Sms = true;
            //}
        }
    }
}
 