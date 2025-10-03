using BusinessObjectLayer.IServices;
using BusinessObjectLayer.Services;
using DataAccessLayer;
using DataAccessLayer.IRepositories;
using DataAccessLayer.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.StaticFiles;


// Load .env from the solution root (parent directory)
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

var builder = WebApplication.CreateBuilder(args);

// Explicitly add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
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

builder.Services.AddDbContext<AICESDbContext>(options =>
    options.UseSqlServer(Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTIONSTRING")));

builder.Services.AddMemoryCache();

// Register Repositories and Services
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();


// Configure Authentication (JWT)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // Set to true after deployment
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuers = new[] { Environment.GetEnvironmentVariable("JWTCONFIG__ISSUERS__0") },
        ValidAudiences = new[] { 
            Environment.GetEnvironmentVariable("JWTCONFIG__AUDIENCES__0"), 
            Environment.GetEnvironmentVariable("JWTCONFIG__AUDIENCES__1") 
        },
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWTCONFIG__KEY")!)
        ),

        ClockSkew = TimeSpan.Zero
    };
});

// Configure CORS (AllowCredentials already enabled for cookies)
builder.Services.AddCors(p => p.AddPolicy("Cors", policy =>
{
    policy.WithOrigins("http://localhost:5173", "https://localhost:7220")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials(); // Required for cookies
}));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AICES API v1");
        c.RoutePrefix = string.Empty; // ??t Swagger ? root[](https://localhost:7220/)
    });
}

// Global exception handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("An unexpected error occurred.");
    });
});

app.UseCors("Cors");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();