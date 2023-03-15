using Backend.Models.External;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using static Backend.Models.External.Appointment;

namespace Backend.Models
{
    [Table("OnlineAppointments")]
    public class OnlineAppointment
    {
        [Key]
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [NotMapped]
        [JsonPropertyName("datetime")]
        public DateTime DateTime { get; set; }

        [NotMapped]
        [JsonPropertyName("ical_data")]
        public string ICalData { get; set; }
        
        [NotMapped]
        [JsonPropertyName("status")]
        public AppointmentStatus Status { get; set; }

        [NotMapped]
        [JsonPropertyName("expected_duration")]
        public TimeSpan ExpectedDuration { get; set; }

        [Required]
        [JsonPropertyName("doctor_id")]
        public Guid DoctorId { get; set; }

        [Required]
        [JsonPropertyName("patient_id")]
        public Guid PatientId { get; set; }

        [Required]
        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [Required]
        [JsonPropertyName("session_url")]
        public string SessionUrl { get; set; }

        [Required]
        [JsonPropertyName("specialty")]
        public DoctorSpecialtyEnum Specialty { get; set; }

        [Required]
        [JsonIgnore]
        public Guid AppointmentId { get; set; }
    }
}
