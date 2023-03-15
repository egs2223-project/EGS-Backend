using Microsoft.OpenApi.Validations.Rules;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Backend.Models
{
    public class Doctor : User
    {
        [Required]
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; }

        [Required]
        [JsonPropertyName("specialties")]
        public List<DoctorSpecialty> Specialties { get; set; }


        //public Doctor() : base()
        //{
        //    OrderId = Guid.NewGuid().ToString();
        //    Specialties = new List<Specialty>();
        //    Specialties.Add(Specialty.Immunology);
        //}
    }
}
