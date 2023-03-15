using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Policy;
using System.Text.Json.Serialization;

namespace Backend.Models
{
    [PrimaryKey(nameof(Id))]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        [Required]
        [JsonPropertyName("email")]
        public string Email { get; set; }

        [Required]
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [Required]
        [JsonPropertyName("date_of_birth")]
        public DateTime DateOfBirth { get; set; }
        
        [Required]
        [JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; }
        
        [Required]
        [JsonPropertyName("address")]
        public string Address { get; set; }
        
        [Required]
        [JsonPropertyName("city")]
        public string City { get; set; }
        
        [Required]
        [JsonPropertyName("region")]
        public string Region { get; set; }
        
        [Required]
        [JsonPropertyName("postal_code")]
        public string PostalCode { get; set; }
        
        [Required]
        [JsonPropertyName("country")]
        public string Country { get; set; }

        //public User()
        //{
        //    Email = "test@test.com";
        //    Name = "name";
        //    DateOfBirth = new DateTime();
        //    PhoneNumber = "1234567890";
        //    Address = "address";
        //    City = "city";
        //    Region = "12345";
        //    PostalCode= "12345";
        //    Country= "12345";
        //}
    }
}
