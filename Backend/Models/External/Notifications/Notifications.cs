using Backend.Models.External.Appointments;
using System.Globalization;
using System.Text;

namespace Backend.Models.External.Notifications
{
    public static class Notifications
    {
        private static CultureInfo culture;
        private static EmailAttachment LogoAttachment;

        static Notifications()
        {
            culture = new("en-US");
            LogoAttachment = new EmailAttachment()
            {
                AttachmentData = Convert.ToBase64String(File.ReadAllBytes("Static/logo.png")),
                AttachmentName = $"DocTalk_logo.png",
                AttachmentMime = "image/png"
            };
        }

        private static async Task SendAppointmentNotification(string message, Patient patient, Doctor doctor, OnlineAppointment appointment)
        {
            using HttpClient httpClient = new();

            if(patient.Preferences.Sms)
            {
                string query = $"{BackendEndpoints.NotificationServiceBaseUrl}/notifications/sms";

                SmsNotification smsNotification = new()
                {
                    SendTo = patient.PhoneNumber,
                    MsgBody = message
                };

                var response = await httpClient.PostAsJsonAsync(query, smsNotification);
                Console.WriteLine(await response.Content.ReadAsStringAsync());
            }

            if(patient.Preferences.Email)
            {
                string query = $"{BackendEndpoints.NotificationServiceBaseUrl}/notifications/email";

                EmailNotification emailNotification = new()
                {
                    Sender = "egs-notify@nextechnology.xyz",
                    Recipients = new string[] { patient.Email },
                    Subject = "DocTalk | Appointments",
                    Body = $""""
                                <img alt="DocTalk" src="cid:DocTalk_logo.png"/>
                                <p>{message}</p>
                            """",
                    Attachments = new EmailAttachment[] 
                    { 
                        LogoAttachment,
                        new EmailAttachment() 
                        { 
                            AttachmentData = Convert.ToBase64String(Encoding.UTF8.GetBytes(appointment.ICalData)),
                            AttachmentName = $"DocTalk_appointment.icl",
                            AttachmentMime = "text/calendar"
                        } 
                    }
                };

                var response = await httpClient.PostAsJsonAsync(query, emailNotification);
                Console.WriteLine(await response.Content.ReadAsStringAsync());
            }
        }

        public static async Task SendNewAppointmentNotification(Patient patient, Doctor doctor, OnlineAppointment appointment)
        {
            string not = $"We're confirming your online doctor's appointment on {appointment.DateTime.ToString("dddd, dd MMMM yyyy", culture)} with Dr. {doctor.Name}";

            await SendAppointmentNotification(not, patient, doctor, appointment);
        }

        public static async Task SendCancelledAppointmentNotifiaction(Patient patient, Doctor doctor, OnlineAppointment appointment)
        {
            string not = $"Your online doctor's appointment on {appointment.DateTime.ToString("dddd, dd MMMM yyyy", culture)} with Dr. {doctor.Name} has been cancelled";

            await SendAppointmentNotification(not, patient, doctor, appointment);
        }
    }
}
