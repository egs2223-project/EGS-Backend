using Backend.Models.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace Backend.Models
{
    public class ServiceDb : DbContext
    {
        public ServiceDb(DbContextOptions<ServiceDb> options)
        : base(options) 
        {
        }

        public DbSet<Doctor> Doctors => Set<Doctor>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<User> Users => Set<User>();
        public DbSet<OnlineAppointment> Appointments => Set<OnlineAppointment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //https://stackoverflow.com/questions/65601304/list-of-enums-in-ef-core

            base.OnModelCreating(modelBuilder);

            //var converter = new EnumCollectionJsonValueConverter<Doctor.Specialty>();
            //var comparer = new CollectionValueComparer<Doctor.Specialty>();

            //modelBuilder.Entity<Doctor>()
            //  .Property(e => e.Specialties)
            //  .HasConversion(converter)
            //  .Metadata.SetValueComparer(comparer);

            modelBuilder.Entity<OnlineAppointment>()
                .Property(b => b.Id)
                .HasDefaultValueSql("newsequentialid()");

            modelBuilder.Entity<User>()
                .Property(b => b.Id)
                .HasDefaultValueSql("newsequentialid()");
        }

        //private class EnumCollectionJsonValueConverter<T> : ValueConverter<ICollection<T>, string> where T : Enum
        //{
        //    public EnumCollectionJsonValueConverter() : base(
        //      v => JsonSerializer.Serialize(v.Select(e => e.ToString()).ToList(), JsonSerializerOptions.Default),
        //      v => JsonSerializer.Deserialize<ICollection<string>>(v, JsonSerializerOptions.Default)
        //        .Select(e => (T)Enum.Parse(typeof(T), e)).ToList())
        //    {
        //    }
        //}

        //private class CollectionValueComparer<T> : ValueComparer<ICollection<T>>
        //{
        //    public CollectionValueComparer() : base((c1, c2) => c1.SequenceEqual(c2),
        //      c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())), c => (ICollection<T>)c.ToHashSet())
        //    {
        //    }
        //}
    }

    public static class ServiceDbExtensions
    {
        public const string AppointmentBaseAddress = "https://localhost:7012/v1";

        //public static async Task<bool> PostOnlineAppointment(this ServiceDb serviceDb, OnlineAppointment onlineAppointment)
        //{
        //    Appointment extAppointment = new()
        //    {
        //        DateTime = onlineAppointment.DateTime,
        //        Status = onlineAppointment.Status,
        //        ExpectedDuration = onlineAppointment.ExpectedDuration,
        //        Recurring = false,
        //        RecurringFrequency = null,
        //        Location = "Online",
        //        Participants = new() { new(onlineAppointment.DoctorId), new(onlineAppointment.PatientId) },
        //        Description = "Online Appointment between a Doctor and a Patient"
        //    };

        //    using HttpClient httpClient = new();
        //    string query = $"{AppointmentBaseAddress}/appointments";

        //    var resp = await httpClient.PostAsJsonAsync(query, extAppointment);

        //    Console.WriteLine($"PostOnlineAppointment response is: {resp.StatusCode}");

        //    string response = await resp.Content.ReadAsStringAsync();

        //    extAppointment = JsonSerializer.Deserialize<Appointment>(response);

        //    onlineAppointment.AppointmentId = extAppointment.Id;

        //    await serviceDb.Appointments.AddAsync(onlineAppointment);
        //    await serviceDb.SaveChangesAsync();

        //    return true;
        //}

        //public static async Task<ICollection<OnlineAppointment>> GetDoctorOnlineAppointments(this ServiceDb serviceDb, long doctor_id, DateTime? from, DateTime? to, int limit, int offset)
        //{
        //    var onlineAppointments = await serviceDb.Appointments.Where(a => a.DoctorId == doctor_id).ToListAsync();

        //    if (onlineAppointments.Count == 0) return new List<OnlineAppointment>();

        //    using HttpClient httpClient = new();

        //    string query = $"{AppointmentBaseAddress}/appointments?participant_id={doctor_id}&limit={limit}&offset={offset}";
        //    if (from != null) query += $"&from={from.Value:O}";
        //    if (to != null) query += $"&to={from.Value:O}";

        //    Console.WriteLine($"GetDoctorOnlineAppointments calling: {query}");

        //    var resp = await httpClient.GetAsync(query);

        //    Console.WriteLine($"GetDoctorOnlineAppointments response is: {resp.StatusCode}");

        //    string response = await resp.Content.ReadAsStringAsync();

        //    ICollection<Appointment> appointments = JsonSerializer.Deserialize<ICollection<Appointment>>(response);

        //    Console.WriteLine($"GetDoctorOnlineAppointments query returned {appointments.Count} results");

        //    foreach (var onlineApp in onlineAppointments)
        //    {
        //        var externalApps = appointments.Where(a => a.Id == onlineApp.AppointmentId);

        //        if (externalApps.Count() != 1) throw new ApplicationException("Inconsistent data with the appointment service");

        //        var externalApp = externalApps.First();

        //        onlineApp.DateTime = externalApp.DateTime;
        //        onlineApp.ICalData = externalApp.ICalData;
        //        onlineApp.Status = externalApp.Status;
        //        onlineApp.ExpectedDuration = externalApp.ExpectedDuration;
        //    }

        //    return onlineAppointments;
        //}
    }
}