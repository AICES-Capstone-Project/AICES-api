using BusinessObjectLayer.IServices.Auth;
using BusinessObjectLayer.Services;
using BusinessObjectLayer.Services.Auth;
using DataAccessLayer;
using DataAccessLayer.IRepositories;
using DataAccessLayer.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json;
using DotNetEnv;
using CloudinaryDotNet;
using BusinessObjectLayer.IServices;
using Microsoft.AspNetCore.Mvc;
using Data.Models.Response;
using Data.Enum;

// ------------------------
// ?? LOAD ENVIRONMENT FILE
// ------------------------
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
    Console.WriteLine($".env file loaded from: {envPath}");
}
else
{
    Console.WriteLine($".env file not found at: {envPath}");
}

// ------------------------
// ?? CREATE BUILDER
// ------------------------
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });


// Customize automatic model validation (400) to return ServiceResponse
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var serviceResponse = new ServiceResponse
        {
            Status = SRStatus.Validation,
            Message = "Validation failed."
        };

        return new ObjectResult(serviceResponse)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AICES API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token (without 'Bearer' prefix)"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();


// ------------------------
// ?? DATABASE CONFIGURATION
// ------------------------
var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTIONSTRING");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("?? Database connection string not found in .env");
}
builder.Services.AddDbContext<AICESDbContext>(options =>
    options.UseNpgsql(connectionString));

// ------------------------
// ?? CLOUDINARY CONFIGURATION
// ------------------------
var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY__CLOUDNAME");
var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY__APIKEY");
var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY__APISECRET");

if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
{
    var account = new Account(cloudName, apiKey, apiSecret);
    var cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    builder.Services.AddSingleton(cloudinary);
    
    // Register CloudinaryHelper as scoped service
    builder.Services.AddScoped<BusinessObjectLayer.Common.CloudinaryHelper>();
    
    Console.WriteLine($"? Cloudinary configured successfully: {cloudName}");
}
else
{
    Console.WriteLine("?? Cloudinary configuration missing in .env file.");
}

// ------------------------
// ?? GOOGLE CLOUD STORAGE CONFIGURATION
// ------------------------
var gcpProjectId = Environment.GetEnvironmentVariable("GCS__PROJECT_ID");
var gcpBucketName = Environment.GetEnvironmentVariable("GCS__BUCKET_NAME");
var gcpCredentialPath = Environment.GetEnvironmentVariable("GCS__CREDENTIAL_PATH");

if (!string.IsNullOrEmpty(gcpBucketName) && !string.IsNullOrEmpty(gcpCredentialPath))
{
    // Configure GCP settings in IConfiguration
    builder.Configuration["GCP:BUCKET_NAME"] = gcpBucketName;
    builder.Configuration["GCP:CREDENTIAL_PATH"] = gcpCredentialPath;
    if (!string.IsNullOrEmpty(gcpProjectId))
    {
        builder.Configuration["GCP:PROJECT_ID"] = gcpProjectId;
    }
    
    // Register GoogleCloudStorageService
    builder.Services.AddSingleton<IGoogleCloudStorageService, GoogleCloudStorageService>();
    
    Console.WriteLine($"✅ Google Cloud Storage configured successfully: {gcpBucketName}");
}
else
{
    Console.WriteLine("⚠️ Google Cloud Storage configuration missing in .env file.");
}

// ------------------------
// ?? REGISTER REPOSITORIES & SERVICES
// ------------------------

// Repositories
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<ICompanyUserRepository, CompanyUserRepository>();
builder.Services.AddScoped<ICompanyDocumentRepository, CompanyDocumentRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IEmploymentTypeRepository, EmploymentTypeRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
// Removed JobCategoryRepository after replacing with Specialization
builder.Services.AddScoped<ISpecializationRepository, SpecializationRepository>();
builder.Services.AddScoped<IJobEmploymentTypeRepository, JobEmploymentTypeRepository>();
builder.Services.AddScoped<ICriteriaRepository, CriteriaRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IBannerConfigRepository, BannerConfigRepository>();
builder.Services.AddScoped<ISkillRepository, SkillRepository>();
builder.Services.AddScoped<IJobSkillRepository, JobSkillRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();

// Services
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<ICompanyUserService, CompanyUserService>();
builder.Services.AddScoped<ICompanyDocumentService, CompanyDocumentService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ISpecializationService, SpecializationService>();
builder.Services.AddScoped<IEmploymentTypeService, EmploymentTypeService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IBannerConfigService, BannerConfigService>();
builder.Services.AddScoped<ISkillService, SkillService>();
builder.Services.AddScoped<IJobSkillService, JobSkillService>();
builder.Services.AddScoped<ICriteriaService, CriteriaService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRoleService, RoleService>();

//  Auth Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();



// ?? JWT AUTHENTICATION CONFIGURATION
// ------------------------
var jwtKey = Environment.GetEnvironmentVariable("JWTCONFIG__KEY");
if (string.IsNullOrEmpty(jwtKey))
{
    Console.WriteLine("?? JWT Key is missing in .env");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // Set true when deploy
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuers = new[] { Environment.GetEnvironmentVariable("JWTCONFIG__ISSUERS__0") },
        ValidAudiences = new[]
        {
            Environment.GetEnvironmentVariable("JWTCONFIG__AUDIENCES__0"),
            Environment.GetEnvironmentVariable("JWTCONFIG__AUDIENCES__1")
        },
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey ?? "DEFAULT_KEY")),
        ClockSkew = TimeSpan.Zero
    };

    // ?? Th�m ?o?n n�y ?? SignalR ??c JWT t? query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            // N?u request ??n SignalR th� l?y token t? query string
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notification"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

    
    // Custom authentication events to handle 401 responses
    //options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    //{
    //    OnChallenge = async context =>
    //    {
    //        // Skip the default challenge behavior
    //        context.HandleResponse();
            
    //        // Create custom ServiceResponse
    //        var serviceResponse = new ServiceResponse
    //        {
    //            Status = SRStatus.Unauthorized,
    //            Message = "Authentication required. Please provide a valid token."
    //        };
            
    //        // Set response headers and content
    //        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    //        context.Response.ContentType = "application/json";
            
    //        var jsonResponse = JsonSerializer.Serialize(serviceResponse, new JsonSerializerOptions
    //        {
    //            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    //            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    //        });
            
    //      await context.Response.WriteAsync(jsonResponse);
    //    },
        
    //    OnAuthenticationFailed = async context =>
    //    {
    //        // Handle authentication failures (invalid token, expired, etc.)
    //        var serviceResponse = new ServiceResponse
    //        {
    //            Status = SRStatus.Unauthorized,
    //            Message = "Invalid or expired token. Please login again."
    //        };
            
    //        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    //        context.Response.ContentType = "application/json";
            
    //        var jsonResponse = JsonSerializer.Serialize(serviceResponse, new JsonSerializerOptions
    //        {
    //            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    //            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    //        });

    //        await context.Response.WriteAsync(jsonResponse);
    //    },

    //    OnForbidden = async context =>
    //    {
    //        // Handle authorization failures (valid token but insufficient permissions)
    //        var serviceResponse = new ServiceResponse
    //        {
    //            Status = SRStatus.Forbidden,
    //            Message = "Access denied. You don't have permission to access this resource."
    //        };

    //        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    //        context.Response.ContentType = "application/json";

    //        var jsonResponse = JsonSerializer.Serialize(serviceResponse, new JsonSerializerOptions
    //        {
    //            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    //            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    //        });

    //        await context.Response.WriteAsync(jsonResponse);
    //    }
    //};


// ------------------------
// ?? CORS CONFIGURATION
// ------------------------
builder.Services.AddCors(p => p.AddPolicy("Cors", policy =>
{
    policy.WithOrigins("http://localhost:5173", "https://localhost:7220", "null")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
}));

builder.Services.AddMemoryCache();

// ------------------------
// ?? BUILD APP
// ------------------------
var app = builder.Build();

// ------------------------
// ?? SWAGGER
// ------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AICES API v1");
        c.RoutePrefix = string.Empty;
    });
}

// ------------------------
// ?? GLOBAL EXCEPTION HANDLER
// ------------------------
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("An unexpected error occurred.");
    });
});

// ------------------------
// ?? MIDDLEWARE PIPELINE
// ------------------------

app.UseCors("Cors");
app.UseHttpsRedirection();
app.UseStaticFiles(); 
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notification");
app.Run();
