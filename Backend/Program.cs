
using Backend.Models;
using Backend.Models.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Data;
using System.Dynamic;
using System.Numerics;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Backend
{
    public class Program
    {
        public static IConfiguration Configuration;

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            Configuration = builder.Configuration;

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options => 
            {
                options.MapType<TimeSpan>(() => new OpenApiSchema { Type = "string", Example = new OpenApiString("00:20:00") });
                options.MapType<DoctorSpecialty>(() => new OpenApiSchema { Type = "string", Example = new OpenApiString(DoctorSpecialtyEnum.Allergiology.ToString()) });

                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Id = "Bearer",
                                Type = ReferenceType.SecurityScheme
                            }
                        },
                        new List<string>()
                    }
                });
            });

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateActor = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["External:Auth:Issuer"],
                    ValidAudience = builder.Configuration["External:Auth:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["External:Auth:Key"])),
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers.Add("Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
            {
                builder.Services.AddDbContext<ServiceDb>(options =>
                {
                    options.UseInMemoryDatabase("ServiceDb");
                });
            }
            else
            {
                builder.Services.AddDbContext<ServiceDb>(options =>
                {
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
                });            
            }

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.MapBackendRoutes();

            app.UseHttpsRedirection();
            app.UseAuthorization();

            app.Run();
        }
    }

    // example token format 
    //{
    //  "sub": "1234567890",
    //  "email": "test@test.com",
    //  "name": "John Doe",
    //  "iat": 1516239022,
    //  "exp": 2016239022,
    //  "aud": "https://localhost:7000/",
    //  "iss": "https://localhost:7000/"
    //}

    public static class BackendEndpoints
    {
        private const string FrontendHomeUrl = "home";
        
        private const string AuthServiceBaseUrl = "https://google.com";
        private const string AppointmentServiceBaseUrl = "https://localhost:7012/v1";
        private const string RTCServiceBaseUrl = "http://localhost:3000";
        private const string NotificationServiceBaseUrl = "https://localhost";

        private static ILogger logger = LoggerFactory.Create(builder => builder.AddConsole()
                        .SetMinimumLevel(LogLevel.Debug)).CreateLogger("BackendEndpoints");

        public static void MapBackendRoutes(this IEndpointRouteBuilder app)
        {
            app.MapGet  ("/v1/login", GetLogin);

            app.MapGet  ("/v1/self", GetSelf);

            app.MapPost ("/v1/doctors", PostDoctor);
            app.MapGet  ("/v1/doctors", GetDoctorSearch);
            app.MapGet  ("/v1/doctors/{doctor_id}", GetDoctorId);
            app.MapPut  ("/v1/doctors/{doctor_id}", PutDoctor);

            app.MapPost ("/v1/patients", PostPatient);
            app.MapGet  ("/v1/patients/{patient_id}", GetPatient);
            app.MapPut  ("/v1/patients/{patient_id}", PutPatient);

            app.MapGet  ("/v1/appointments", GetOnlineAppointments);
            app.MapPost ("/v1/appointments", PostOnlineAppointment);
            app.MapPut  ("/v1/appointments/{appointment_id}", PutOnlineAppointment);
        }

        /// <summary>
        /// Called by the Frontend to login a user.
        /// Redirects to the authentication service
        /// </summary>
        /// <response code="302">Redirects to the authentication service</response>
        [ProducesResponseType(302)]
        private static void GetLogin(HttpContext ctx)
        {
            ctx.Response.Redirect($"{AuthServiceBaseUrl}/redirect_url={FrontendHomeUrl}");
        }

        /// <summary>
        /// Called by the Frontend post authentication to get the logged in user data
        /// </summary>
        /// <returns>
        /// Either a Doctor or Patient user object
        /// </returns>
        /// <remarks>
        /// Returns either a Doctor or Patient user object
        /// </remarks>
        /// <response code="200">Ok</response>
        /// <response code="403">Not authorized</response>
        /// <response code="404">Not registered. You probably should</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> GetSelf(HttpContext ctx, ServiceDb db)
        {
            string email = ctx.User.FindFirstValue(ClaimTypes.Email);

            var user = await db.Users.Where(u => u.Email == email).SingleOrDefaultAsync();

            if (user is null)
            {
                return Results.NotFound();
            }

            dynamic result = new ExpandoObject();

            if (user is Patient p)
            {
                result.user_type = "Patient";
                await db.Patients.Entry(p).Reference(u => u.Preferences).LoadAsync();
            }
            else
            {
                result.user_type = "Doctor";
                await db.Doctors.Entry(user as Doctor).Collection(u => u.Specialties).LoadAsync();
            }
            result.user_data = user;

            return Results.Ok(result);
        }

        /// <summary>
        /// Registers a new doctor user
        /// </summary>
        /// <param name="doctor">A Doctor user object</param>
        /// <returns>A newly created Doctor</returns>
        /// <response code="201">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        /// <response code="409">When the user email is already registered</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> PostDoctor(Doctor doctor, HttpContext ctx, ServiceDb db)
        {
            doctor.Id = Guid.Empty;
            
            if (doctor.Email != ctx.User.FindFirstValue(ClaimTypes.Email))
            {
                return Results.Forbid();
            }

            if (await db.Users.Where(d => d.Email == doctor.Email).SingleOrDefaultAsync() is not null)
            {
                return Results.Conflict($"e-mail {doctor.Email} is already registered");
            }

            db.Doctors.Add(doctor);
            await db.SaveChangesAsync();

            return Results.Created($"/doctors/{doctor.Id}", doctor);
        }

        /// <summary>
        /// Called by the Frontend to register a new patient user
        /// </summary>
        /// <param name="patient">A Pacient user object</param>
        /// <returns>A newly created Patient</returns>
        /// <response code="201">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        /// <response code="409">When the user email is already registered</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> PostPatient(Patient patient, HttpContext ctx, ServiceDb db)
        {
            patient.Id = Guid.Empty;

            if (patient.Email != ctx.User.FindFirstValue(ClaimTypes.Email))
            {
                return Results.Forbid();
            }

            if (await db.Users.Where(p => p.Email == patient.Email).FirstOrDefaultAsync() is not null)
            {
                return Results.Conflict($"e-mail {patient.Email} is already registered");
            }

            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            return Results.Created($"/patients/{patient.Id}", patient);
        }

        /// <summary>
        /// Gets doctors based on search criteria
        /// </summary>
        /// <remarks>
        /// Personal doctor info will be redacted.
        /// </remarks>
        /// <param name="specialties">An array of Doctor Specialties</param>
        /// <param name="name">The name of a Doctor</param>
        /// <param name="orderId">The Medical Order Doctor id</param>
        /// <param name="limit">The maximum number of elements to return</param>
        /// <param name="offset">The offset of elements to return</param>
        /// <returns>A list of the Doctors matching the search criteria</returns>
        /// <response code="200">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        /// <response code="400">Bad Request</response>
        private static async Task<ICollection<Doctor>> GetDoctorSearch(ServiceDb db, [FromQuery] DoctorSpecialtyEnum[]? specialties, string? name, string? orderId, int limit = 50, int offset = 0)
        {
            var doctors = db.Doctors.AsQueryable();

            if (name != null) doctors = doctors.Where(d => d.Name == name);
            if (orderId != null) doctors = doctors.Where(d => d.OrderId == orderId);

            if (specialties != null)
            {
                var specialtiesList = specialties.ToList();
                doctors = doctors.Where(d => d.Specialties.Any(s => specialtiesList.Contains(s.Specialty)));
            }

            return await doctors.Include(d => d.Specialties).Skip(offset).Take(limit).ToListAsync();
        }

        /// <summary>
        /// Gets a Doctor user object by Id
        /// </summary>
        /// <remarks>
        /// If called by a patient, personal user info will be redacted. If called by another doctor 403 is returned.
        /// </remarks>
        /// <param name="doctor_id">A Doctor Id</param>
        /// <returns>A Doctors object</returns>
        /// <response code="200">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Not Found</response>
        [ProducesResponseType(typeof(Doctor), StatusCodes.Status200OK)]
        private static async Task<IResult> GetDoctorId(Guid doctor_id, ServiceDb db)
        {
            return await db.Doctors.Where(d => d.Id == doctor_id).Include(d => d.Specialties).SingleOrDefaultAsync()
                       is Doctor doctor ? Results.Ok(doctor) : Results.NotFound();
        }

        /// <summary>
        /// Updates a Doctor user object by Id
        /// </summary>
        /// <remarks>
        /// This operation can only be called by the Doctor user himself.
        /// Some fields can not be changed such as name and email. Any changes to these will be ignored
        /// </remarks>
        /// <param name="doctor_id">A Doctor Id</param>
        /// <param name="inDoctor">The updated Doctor user object</param>
        /// <response code="202">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Not Found</response>
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> PutDoctor(Guid doctor_id, Doctor inDoctor, HttpContext ctx, ServiceDb db)
        {
            var doctor = await db.Doctors.Where(d => d.Id == doctor_id).Include(d => d.Specialties).SingleOrDefaultAsync();

            if (doctor is null) return Results.NotFound();

            string email = ctx.User.FindFirstValue(ClaimTypes.Email);
            if (email != doctor.Email) return Results.Forbid();

            doctor.Address = inDoctor.Address;
            doctor.City = inDoctor.City;
            doctor.Country = inDoctor.Country;
            doctor.DateOfBirth = inDoctor.DateOfBirth;
            doctor.PhoneNumber = inDoctor.PhoneNumber;
            doctor.PostalCode = inDoctor.PostalCode;
            doctor.Region = inDoctor.Region;
            doctor.Specialties = inDoctor.Specialties;

            await db.SaveChangesAsync();

            return Results.Accepted($"/doctors/{doctor_id}", doctor);
        }

        /// <summary>
        /// Gets a Patient user object by Id
        /// </summary>
        /// <remarks>
        /// If called by a doctor, personal user info will be redacted. If called by another patient 403 is returned.
        /// </remarks>
        /// <param name="patient_id">A Patient Id</param>
        /// <returns>A Patient object</returns>
        /// <response code="200">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Not Found</response>
        private static async Task<IResult> GetPatient(Guid patient_id, ServiceDb db)
        {
            return (await db.Patients.Where(p => p.Id == patient_id).Include(i => i.Preferences).ToListAsync()).FirstOrDefault()
                   is Patient patient ? Results.Ok(patient) : Results.NotFound();
        }

        /// <summary>
        /// Updates a Patient user
        /// </summary>
        /// <remarks>
        /// This operation can only be called by the Patient user himself.
        /// Some fields can not be changed such as name and email. Any changes to these will be ignored
        /// </remarks>
        /// <param name="patient_id">A Patient Id</param>
        /// <param name="inPatient">The updated Patient user object</param>
        /// <response code="202">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Not Found</response>
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> PutPatient(Guid patient_id, Patient inPatient, HttpContext ctx, ServiceDb db)
        {
            var patient = (await db.Patients.Where(p => p.Id == patient_id).Include(i => i.Preferences).ToListAsync()).FirstOrDefault();

            if (patient is null) return Results.NotFound();

            string email = ctx.User.FindFirstValue(ClaimTypes.Email);
            if (email != patient.Email) return Results.Forbid();

            patient.Address = inPatient.Address;
            patient.City = inPatient.City;
            patient.Country = inPatient.Country;
            patient.DateOfBirth = inPatient.DateOfBirth;
            patient.PhoneNumber = inPatient.PhoneNumber;
            patient.PostalCode = inPatient.PostalCode;
            patient.Region = inPatient.Region;
            patient.Preferences.Sms = inPatient.Preferences.Sms;
            patient.Preferences.Email = inPatient.Preferences.Email;

            await db.SaveChangesAsync();

            return Results.Accepted($"/patients/{patient_id}", patient);
        }

        /// <summary>
        /// Registers a new OnlineAppointment. Must be called by a patient
        /// </summary>
        /// <param name="onlineAppointment">A OnlineAppointment object</param>
        /// <returns>A newly created OnlineAppointment</returns>
        /// <response code="201">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        /// <response code="409">The new appointment conflicts with an existing one</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> PostOnlineAppointment(OnlineAppointment onlineAppointment, HttpContext ctx, ServiceDb db)
        {
            string email = ctx.User.FindFirstValue(ClaimTypes.Email);

            var user = await db.Users.Where(u => u.Email == email).SingleOrDefaultAsync();
            if (user is null) return Results.Forbid();
            if (user is Doctor) return Results.Forbid();
            if (user.Id != onlineAppointment.PatientId) return Results.Forbid();

            onlineAppointment.Id = Guid.Empty;
            Appointment extAppointment = new()
            {
                DateTime = onlineAppointment.DateTime,
                Status = onlineAppointment.Status,
                ExpectedDuration = onlineAppointment.ExpectedDuration,
                Recurring = false,
                RecurringFrequency = null,
                Location = "Online",
                Participants = new() { new(onlineAppointment.DoctorId), new(onlineAppointment.PatientId) },
                Description = "Online Appointment between a Doctor and a Patient"
            };

            using HttpClient httpClient = new();

            string rtcQuery = $"{RTCServiceBaseUrl}/video-call";
            string appointmentQuery = $"{AppointmentServiceBaseUrl}/appointments";

            logger.LogInformation($"PostOnlineAppointment getting a video call id");

            string response = await httpClient.GetStringAsync(rtcQuery);
            response = JsonDocument.Parse(response).RootElement.GetProperty("videoCallId").GetGuid().ToString();

            logger.LogInformation($"PostOnlineAppointment got response: {response}");
            onlineAppointment.SessionUrl = $"{RTCServiceBaseUrl}/room={response}";

            logger.LogInformation($"PostOnlineAppointment posting an Appointment to: {appointmentQuery}");

            var resp = await httpClient.PostAsJsonAsync(appointmentQuery, extAppointment);

            logger.LogInformation($"PostOnlineAppointment response is: {resp.StatusCode}");

            response = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode != System.Net.HttpStatusCode.Created)
            {
                return Results.Conflict();
            }

            extAppointment = JsonSerializer.Deserialize<Appointment>(response);

            onlineAppointment.AppointmentId = extAppointment.Id;

            await db.Appointments.AddAsync(onlineAppointment);
            await db.SaveChangesAsync();

            return Results.Created("/", onlineAppointment);
        }

        /// <summary>
        /// Gets appointments the given logged in user is a participant of, or from a specific doctor
        /// Allows for filtering parameters and paging
        /// </summary>
        /// <remarks>
        /// When getting the appointments of a doctor, personal info will be redacted
        /// </remarks>
        /// <returns>A list of the OnlineAppointment objects</returns>
        /// <param name="doctor_id">Retrive appointments from this doctor</param>
        /// <param name="status">Retrieve appointments with only this status</param>
        /// <param name="from">Retrieve appointments from this date onwards</param>
        /// <param name="to">Retrieve appointments from this date backwards</param>
        /// <param name="limit">The maximum number of appointments to be returned on a single request</param>
        /// <param name="offset">The offset of results to be returned</param>
        /// <response code="200">On a successfull operation</response>
        /// <response code="403">Forbidden</response>
        [ProducesResponseType(typeof(ICollection<OnlineAppointment>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> GetOnlineAppointments(HttpContext ctx, ServiceDb db, Guid? doctor_id, Appointment.AppointmentStatus? status, DateTime? from, DateTime? to, int limit = 50, int offset = 0)
        {
            string email = ctx.User.FindFirstValue(ClaimTypes.Email);

            var user = await db.Users.Where(u => u.Email == email).SingleOrDefaultAsync();
            if (user == null) return Results.Forbid();

            var onlineAppointmentsQuery = db.Appointments.AsQueryable();
            Guid participant_id;
            bool doctor = false;

            if (doctor_id is not null)
            {
                participant_id = doctor_id.Value;
                doctor = true;
            }
            else
            {
                if (user is Doctor d)
                {
                    participant_id = d.Id;
                    doctor = true;
                }
                else participant_id = (user as Patient).Id;
            }

            if (doctor) onlineAppointmentsQuery = onlineAppointmentsQuery.Where(a => a.DoctorId == participant_id);
            else onlineAppointmentsQuery = onlineAppointmentsQuery.Where(a => a.PatientId == participant_id);

            var onlineAppointments = await onlineAppointmentsQuery.ToListAsync();
            if (onlineAppointments.Count == 0) return Results.Ok(new List<OnlineAppointment>());

            using HttpClient httpClient = new();

            string query = $"{AppointmentServiceBaseUrl}/appointments?participant_id={participant_id}&limit={limit}&offset={offset}";
            if (from.HasValue) query += $"&from={from.Value:O}";
            if (to.HasValue) query += $"&to={from.Value:O}";

            logger.LogInformation($"GetDoctorOnlineAppointments calling: {query}");

            ICollection<Appointment> appointments = await httpClient.GetFromJsonAsync<ICollection<Appointment>>(query);

            logger.LogInformation($"GetDoctorOnlineAppointments query returned {appointments.Count()} results");

            foreach (var onlineApp in onlineAppointments)
            {
                var externalApps = appointments.Where(a => a.Id == onlineApp.AppointmentId);

                if (externalApps.Count() != 1)
                {
                    logger.LogError("Inconsistent data with the appointment service");
                }

                var externalApp = externalApps.First();

                onlineApp.DateTime = externalApp.DateTime;
                onlineApp.ICalData = externalApp.ICalData;
                onlineApp.Status = externalApp.Status;
                onlineApp.ExpectedDuration = externalApp.ExpectedDuration;
            }

            if(status != null) onlineAppointments = onlineAppointments.Where(a => a.Status == status).ToList();

            return Results.Ok(onlineAppointments);
        }

        /// <summary>
        /// Updates an OnlineAppointment object
        /// </summary>
        /// <remarks>
        /// The user calling this endpoint must either be the Doctor or Patient assigned to this Appointment.
        /// Most fields are ignored and cannot be changed. Status and summary can.
        /// </remarks>
        /// <param name="appointment_id">The id of the appointment to be updated</param>
        /// <param name="inOnlineAppointment">The appointment object</param>
        /// <response code="204">Updated</response>
        /// <response code="403">Unauthorized</response>
        /// <response code="404">Not Found</response>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> PutOnlineAppointment(Guid appointment_id, OnlineAppointment inOnlineAppointment, HttpContext ctx, ServiceDb db)
        {
            var onlineAppointment = await db.Appointments.FindAsync(appointment_id);

            if (onlineAppointment is null) return Results.NotFound();

            string email = ctx.User.FindFirstValue(ClaimTypes.Email);
            var user = db.Users.Where(u => u.Email == email).SingleOrDefault();

            if (user is null) return Results.Forbid();

            if (user is Doctor d)
            {
                if (d.Id != onlineAppointment.DoctorId) return Results.Forbid();
            }
            else if ((user as Patient).Id != onlineAppointment.PatientId) return Results.Forbid();

            onlineAppointment.Status = inOnlineAppointment.Status;
            onlineAppointment.Summary = inOnlineAppointment.Summary;

            using HttpClient httpClient = new();

            string query = $"{AppointmentServiceBaseUrl}/appointments/{onlineAppointment.AppointmentId}";

            Appointment appointment = await httpClient.GetFromJsonAsync<Appointment>(query);
            appointment.Status = onlineAppointment.Status;

            await httpClient.PutAsJsonAsync(query, appointment);

            await db.SaveChangesAsync();

            return Results.NoContent();
        }
    }
}