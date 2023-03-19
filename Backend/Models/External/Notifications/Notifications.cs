using System.Text;

namespace Backend.Models.External.Notifications
{
    public static class Notifications
    {
        public static async Task SendAppointmentNotification(Patient patient, Doctor doctor, OnlineAppointment appointment)
        {
            using HttpClient httpClient = new();

            if(patient.Preferences.Sms)
            {
                string query = $"{BackendEndpoints.NotificationServiceBaseUrl}/notifications/sms";

                SmsNotification smsNotification = new()
                {
                    SendTo = patient.PhoneNumber,
                    MsgBody = $"MediCall24: We're confirming your online doctor's appointment at {appointment.DateTime:dddd, dd MMMM yyyy} with Dr. {doctor.Name}"
                };

                var response = await httpClient.PostAsJsonAsync(query, smsNotification);
                //Console.WriteLine(await response.Content.ReadAsStringAsync());
            }

            if(patient.Preferences.Email)
            {
                string query = $"{BackendEndpoints.NotificationServiceBaseUrl}/notifications/email";

                EmailNotification emailNotification = new()
                {
                    SendTo = patient.Email,
                    Subject = "MediCall24 | Appointments",
                    Text = $"We're confirming your online doctor's appointment at {appointment.DateTime:dddd, dd MMMM yyyy} with Dr. {doctor.Name}",
                    Html = "<p><\\p>",
                    Attachments = new EmailAttachment[] 
                    { 
                        new EmailAttachment() 
                        { 
                            Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(appointment.ICalData)),
                            Filename = $"MediCall24_appointment.ical",
                            Type = "text/calendar"
                        } 
                    }
                };

                var response = await httpClient.PostAsJsonAsync(query, emailNotification);
                //Console.WriteLine(await response.Content.ReadAsStringAsync());
            }
        }
    }
}
