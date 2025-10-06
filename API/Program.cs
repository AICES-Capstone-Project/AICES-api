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
using DotNetEnv;
using CloudinaryDotNet;
using BusinessObjectLayer.IServices;

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

builder.Services.AddControllers();
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

// ------------------------
// ?? DATABASE CONFIGURATION
// ------------------------
var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTIONSTRING");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("?? Database connection string not found in .env");
}
builder.Services.AddDbContext<AICESDbContext>(options =>
    options.UseSqlServer(connectionString));

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
    Console.WriteLine($"? Cloudinary configured successfully: {cloudName}");
}
else
{
    Console.WriteLine("?? Cloudinary configuration missing in .env file.");
}

// ------------------------
// ?? REGISTER REPOSITORIES & SERVICES
// ------------------------
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

// Auth Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Other Services
builder.Services.AddScoped<IProfileService, ProfileService>();

// ------------------------
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
});

// ------------------------
// ?? CORS CONFIGURATION
// ------------------------
builder.Services.AddCors(p => p.AddPolicy("Cors", policy =>
{
    policy.WithOrigins("http://localhost:5173", "https://localhost:7220")
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
app.Run();
