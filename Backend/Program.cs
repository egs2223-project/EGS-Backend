
using Backend.Models;
using Backend.Models.External.Appointments;
using Backend.Models.External.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Data;
using System.Dynamic;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {            
            var builder = WebApplication.CreateBuilder(args);
            string? dbConnStr = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") != null ?
                Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") : builder.Configuration.GetConnectionString("DefaultConnection");

            string? FrontendHomeUrl = Environment.GetEnvironmentVariable("FRONTEND_HOME_URL") != null ?
                Environment.GetEnvironmentVariable("FRONTEND_HOME_URL") : builder.Configuration["External:Frontend:BaseUrl"];
            
            string? AuthServiceBaseUrl = Environment.GetEnvironmentVariable("AUTH_SERVICE_BASE_URL") != null ?
                Environment.GetEnvironmentVariable("AUTH_SERVICE_BASE_URL") : builder.Configuration["External:AuthService:BaseUrl"];
            
            string? AppointmentServiceBaseUrl = Environment.GetEnvironmentVariable("APPOINTMENT_SERVICE_BASE_URL") != null ?
                Environment.GetEnvironmentVariable("APPOINTMENT_SERVICE_BASE_URL") : builder.Configuration["External:AppointmentService:BaseUrl"];

            Console.WriteLine("AppointmentServiceBaseUrl " + AppointmentServiceBaseUrl);
            
            string? RTCServiceBaseUrl = Environment.GetEnvironmentVariable("WEBRTC_SERVICE_BASE_URL") != null ?
                Environment.GetEnvironmentVariable("WEBRTC_SERVICE_BASE_URL") : builder.Configuration["External:WebRTCService:BaseUrl"];
            
            string? NotificationServiceBaseUrl = Environment.GetEnvironmentVariable("NOTIFICATION_SERVICE_BASE_URL") != null ?
                Environment.GetEnvironmentVariable("NOTIFICATION_SERVICE_BASE_URL") : builder.Configuration["External:NotificationService:BaseUrl"];

            string? AuthServiceJWTKey = Environment.GetEnvironmentVariable("AUTH_SERVICE_JWT_KEY") != null ?
                Environment.GetEnvironmentVariable("AUTH_SERVICE_JWT_KEY") : builder.Configuration["External:AuthService:Key"];

            string? AuthServiceJWTIssuer = Environment.GetEnvironmentVariable("AUTH_SERVICE_JWT_ISSUER") != null ?
                Environment.GetEnvironmentVariable("AUTH_SERVICE_JWT_ISSUER") : builder.Configuration["External:AuthService:Issuer"];
            
            string? AuthServiceJWTAudience = Environment.GetEnvironmentVariable("AUTH_SERVICE_JWT_AUDIENCE") != null ?
                Environment.GetEnvironmentVariable("AUTH_SERVICE_JWT_AUDIENCE") : builder.Configuration["External:AuthService:Audience"];

            if(dbConnStr == null) throw new ArgumentNullException("Failed to find a connection string to the Database");
            if(FrontendHomeUrl == null) throw new ArgumentNullException(nameof(FrontendHomeUrl));
            if(AuthServiceBaseUrl == null) throw new ArgumentNullException(nameof(AuthServiceBaseUrl));
            if(AppointmentServiceBaseUrl == null) throw new ArgumentNullException(nameof(AppointmentServiceBaseUrl));
            if(RTCServiceBaseUrl == null) throw new ArgumentNullException(nameof(RTCServiceBaseUrl));
            if(NotificationServiceBaseUrl == null) throw new ArgumentNullException(nameof(NotificationServiceBaseUrl));
            if(AuthServiceJWTKey == null) throw new ArgumentNullException(nameof(AuthServiceJWTKey));
            if(AuthServiceJWTIssuer == null) throw new ArgumentNullException(nameof(AuthServiceJWTIssuer));
            if(AuthServiceJWTAudience == null) throw new ArgumentNullException(nameof(AuthServiceJWTAudience));

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

            builder.Services.AddCors(o => o.AddPolicy("MyPolicy", builder =>
            {
                builder.WithOrigins(FrontendHomeUrl, FrontendHomeUrl.Replace("http://", "https://"))
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials();
            }));

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateActor = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = AuthServiceJWTIssuer,
                    ValidAudience = AuthServiceJWTAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthServiceJWTKey)),
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
                    },

                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.ContainsKey("jwt"))
                        {
                            context.Token = context.Request.Cookies["jwt"];
                        }
                        else
                        {
                            context.Token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddDbContext<ServiceDb>(options =>
            {
                options.UseSqlServer(dbConnStr, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure();
                });
            });

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ServiceDb>();
                db.Database.Migrate();
            }
            app.UseCors("MyPolicy");

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.MapBackendRoutes(FrontendHomeUrl, AuthServiceBaseUrl, AppointmentServiceBaseUrl, RTCServiceBaseUrl, NotificationServiceBaseUrl);

            app.UseHttpsRedirection();
            app.UseAuthorization();

            app.Run();
        }
    }

    /* example token format 
    {
      "sub": "1234567890",
      "email": "patient@example.com",
      "name": "John Doe",
      "iat": 1516239022,
      "exp": 2016239022,
      "aud": "https://localhost:7000/",
      "iss": "https://localhost:7000/"
    }
    */

    public static class BackendEndpoints
    {
        private static string FrontendHomeUrl = string.Empty;  
        public static string AuthServiceBaseUrl = string.Empty;
        public static string AppointmentServiceBaseUrl = string.Empty;
        public static string RTCServiceBaseUrl = string.Empty;
        public static string NotificationServiceBaseUrl = string.Empty;

        private static ILogger logger = LoggerFactory.Create(builder => builder.AddConsole()
                        .SetMinimumLevel(LogLevel.Debug)).CreateLogger("BackendEndpoints");

        public static void MapBackendRoutes(this IEndpointRouteBuilder app, 
                                            string FrontendHomeUrl,
                                            string AuthServiceBaseUrl,
                                            string AppointmentServiceBaseUrl,
                                            string RTCServiceBaseUrl,
                                            string NotificationServiceBaseUrl
                                            )
        {
            BackendEndpoints.FrontendHomeUrl = FrontendHomeUrl;
            BackendEndpoints.AuthServiceBaseUrl = AuthServiceBaseUrl;
            BackendEndpoints.AppointmentServiceBaseUrl = AppointmentServiceBaseUrl;
            BackendEndpoints.RTCServiceBaseUrl = RTCServiceBaseUrl;
            BackendEndpoints.NotificationServiceBaseUrl = NotificationServiceBaseUrl;
            
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
            ctx.Response.Redirect($"{AuthServiceBaseUrl}/login?redirect_url={FrontendHomeUrl}");
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
        /// <response code="401">Not authorized</response>
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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<ICollection<Doctor>> GetDoctorSearch(ServiceDb db, [FromQuery] DoctorSpecialtyEnum[]? specialties, string? name, string? orderId, int limit = 50, int offset = 0)
        {
            var doctors = db.Doctors.AsQueryable();

            if (name != null) doctors = doctors.Where(d => d.Name == name);
            if (orderId != null) doctors = doctors.Where(d => d.OrderId == orderId);

            if (specialties != null && specialties.Length > 0)
            {
                var specialtiesList = specialties.ToList();
                doctors = doctors.Where(d => d.Specialties.Any(s => specialtiesList.Contains(s.Specialty)));
            }

            var results = await doctors.Include(d => d.Specialties).OrderBy(d => d.Id).Skip(offset).Take(limit).ToListAsync();
            results.ForEach((d) =>
            {
                d.Address = "[REDACTED]";
                d.City = "[REDACTED]";
                d.PostalCode = "[REDACTED]";
                d.PhoneNumber = "[REDACTED]";
                d.Email = "[REDACTED]";
            });

            return results;
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
        private static async Task<IResult> GetDoctorId(Guid doctor_id, HttpContext ctx, ServiceDb db)
        {
            var doctor = await db.Doctors.Where(p => p.Id == doctor_id).Include(d => d.Specialties).SingleOrDefaultAsync();
            if (doctor is null) return Results.NotFound();

            string email = ctx.User.FindFirstValue(ClaimTypes.Email);
            var user = await db.Users.Where(u => u.Email == email).SingleOrDefaultAsync();
            if (user is null) return Results.NotFound();
            if (user is Doctor d && doctor.Id != d.Id) return Results.Forbid();

            if (user is Patient p)
            {
                doctor.Address = "[REDACTED]";
                doctor.City = "[REDACTED]";
                doctor.PostalCode = "[REDACTED]";
                doctor.PhoneNumber = "[REDACTED]";
                doctor.Email = "[REDACTED]";
            }

            return Results.Ok(doctor);
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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> GetPatient(Guid patient_id, HttpContext ctx, ServiceDb db)
        {
            var patient = await db.Patients.Where(p => p.Id == patient_id).Include(i => i.Preferences).SingleOrDefaultAsync();
            if (patient is null) return Results.NotFound();

            string email = ctx.User.FindFirstValue(ClaimTypes.Email);
            var user = await db.Users.Where(u => u.Email == email).SingleOrDefaultAsync();
            if (user is null) return Results.NotFound();
            if (user is Patient p && patient.Id != p.Id) return Results.Forbid();

            if (user is Doctor d)
            {
                patient.Address = "[REDACTED]";
                patient.City = "[REDACTED]";
                patient.PostalCode = "[REDACTED]";
                patient.PhoneNumber = "[REDACTED]";
                patient.Email = "[REDACTED]";
            }

            return Results.Ok(patient);
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
        /// <response code="404">Doctor not found</response>
        /// <response code="409">The new appointment conflicts with an existing one</response>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        private static async Task<IResult> PostOnlineAppointment(OnlineAppointment onlineAppointment, HttpContext ctx, ServiceDb db)
        {
            string email = ctx.User.FindFirstValue(ClaimTypes.Email);

            var user = await db.Users.Where(u => u.Email == email).SingleOrDefaultAsync();
            if (user is null) return Results.Forbid();
            if (user is Doctor) return Results.Forbid();
            if (user.Id != onlineAppointment.PatientId) return Results.Forbid();

            Patient patient = user as Patient;
            await db.Patients.Entry(patient).Reference(u => u.Preferences).LoadAsync();
            Doctor doctor = await db.Doctors.Where(u => u.Id == onlineAppointment.DoctorId).SingleOrDefaultAsync();
            if (doctor is null) return Results.NotFound("Doctor not found");

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
                Description = $"DocTalk | {onlineAppointment.Specialty} appointment with Dr. {doctor.Name}"
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

            if (resp.StatusCode != HttpStatusCode.Created)
            {
                return Results.Conflict();
            }

            extAppointment = JsonSerializer.Deserialize<Appointment>(response);

            onlineAppointment.AppointmentId = extAppointment.Id;
            onlineAppointment.ICalData = extAppointment.ICalData;

            await db.Appointments.AddAsync(onlineAppointment);
            await db.SaveChangesAsync();

            await Notifications.SendNewAppointmentNotification(patient, doctor, onlineAppointment);

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
            if (to.HasValue) query += $"&to={to.Value:O}";

            logger.LogInformation($"GetDoctorOnlineAppointments calling: {query}");

            ICollection<Appointment> appointments = await httpClient.GetFromJsonAsync<ICollection<Appointment>>(query);

            logger.LogInformation($"GetDoctorOnlineAppointments query returned {appointments.Count()} results");

            foreach (var onlineApp in onlineAppointments.ToList())
            {
                var externalApps = appointments.Where(a => a.Id == onlineApp.AppointmentId);

                var externalApp = externalApps.FirstOrDefault();
                if(externalApp == null)
                {
                    onlineAppointments.Remove(onlineApp);
                    continue;
                }

                onlineApp.DateTime = externalApp.DateTime;
                onlineApp.ICalData = externalApp.ICalData;
                onlineApp.Status = externalApp.Status;
                onlineApp.ExpectedDuration = externalApp.ExpectedDuration;
            }

            if (status != null) onlineAppointments = onlineAppointments.Where(a => a.Status == status).ToList();
            if (doctor && user.Id != participant_id) onlineAppointments.ForEach((a) =>
            {
                a.PatientId = Guid.Empty;
                a.ICalData = "REDACTED";
                a.Summary = "REDACTED";
                a.Reason = "REDACTED";
                a.SessionUrl = "REDACTED";
            });

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
        /// <response code="403">Forbidden</response>
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
            var user = await db.Users.Where(u => u.Email == email).SingleOrDefaultAsync();

            if (user is null) return Results.Forbid();

            if (user is Doctor d) { if (d.Id != onlineAppointment.DoctorId) { return Results.Forbid(); } }
            else if ((user as Patient).Id != onlineAppointment.PatientId) { return Results.Forbid(); }

            using HttpClient httpClient = new();
            string query = $"{AppointmentServiceBaseUrl}/appointments/{onlineAppointment.AppointmentId}";

            Appointment extAppointment = await httpClient.GetFromJsonAsync<Appointment>(query);
            if (extAppointment.Status == Appointment.AppointmentStatus.Cancelled) return Results.Forbid();
            extAppointment.Status = inOnlineAppointment.Status;

            await httpClient.PutAsJsonAsync(query, extAppointment);
            extAppointment = await httpClient.GetFromJsonAsync<Appointment>(query);

            onlineAppointment.ICalData = extAppointment.ICalData;
            onlineAppointment.Summary = inOnlineAppointment.Summary;
            onlineAppointment.DateTime = extAppointment.DateTime;
            if (onlineAppointment.Status != Appointment.AppointmentStatus.Cancelled && inOnlineAppointment.Status == Appointment.AppointmentStatus.Cancelled)
            {
                Patient patient = await db.Patients.Where(p => p.Id == onlineAppointment.PatientId).Include(p => p.Preferences).SingleAsync();
                Doctor doctor = await db.Doctors.Where(d => d.Id == onlineAppointment.DoctorId).SingleAsync();
                await Notifications.SendCancelledAppointmentNotifiaction(patient, doctor, onlineAppointment);
            }
            onlineAppointment.Status = extAppointment.Status;

            await db.SaveChangesAsync();

            return Results.NoContent();
        }
    }
}