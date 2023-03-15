using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Models
{
    [JsonConverter(typeof(DoctorSpecialtyJsonConverter))]
    public class DoctorSpecialty
    {
        [Key]
        public long Id { get; set; }
        public DoctorSpecialtyEnum Specialty { get; set; }

        public DoctorSpecialty(DoctorSpecialtyEnum specialty)
        {
            Specialty = specialty;  
        }

        public static bool TryParse(string json, out DoctorSpecialty doctorSpecialty)
        {
            doctorSpecialty = new DoctorSpecialty(JsonSerializer.Deserialize<DoctorSpecialtyEnum>(json));
            return true;
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DoctorSpecialtyEnum
    {
        Allergiology,
        Immunology,
        Anesthesiology,
        Dermathology,
        DiagnosticRadiology,
        EmergencyMedicine,
        InternalMedicine,
        MedicalGenetics,
        Neurology,
        NuclearMedicine,
        Obstetrics,
        Gynecology,
        Ophthalnology,
        Pathology,
        Pediatrics,
        PhysicalMedicine,
        PreventiveMedicine,
        Psychiatry,
        RadiationOncology,
        Surgery,
        Urology
    }

    public class DoctorSpecialtyJsonConverter : JsonConverter<DoctorSpecialty>
    {
        public override DoctorSpecialty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new((DoctorSpecialtyEnum)Enum.Parse(typeof(DoctorSpecialtyEnum), reader.GetString()));
        }

        public override void Write(Utf8JsonWriter writer, DoctorSpecialty value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Specialty.ToString());
        }
    }
}
