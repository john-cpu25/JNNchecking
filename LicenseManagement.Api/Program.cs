using System.Text;
using LicenseManagement.Application.Interfaces;
using LicenseManagement.Domain.Entities;
using LicenseManagement.Domain.Enums;
using LicenseManagement.Infrastructure.Data;
using LicenseManagement.Infrastructure.Security;
using LicenseManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration: Shared SQLite DB at server/licenses.db
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=licenses.db";

string sharedDb = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "licenses.db"));
if (connectionString.Contains("licenses.db") && !Path.IsPathRooted(connectionString.Replace("Data Source=", "").Trim()))
{
    connectionString = $"Data Source={sharedDb}";
}

bool useInMemoryDb = builder.Configuration.GetValue<bool>("UseInMemoryDb") ||
                     builder.Environment.IsEnvironment("Testing") ||
                     string.Equals(Environment.GetEnvironmentVariable("USE_IN_MEMORY_DB"), "true", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<LicenseDbContext>(options =>
{
    if (useInMemoryDb)
    {
        options.UseInMemoryDatabase("LicenseDb");
    }
    else if (connectionString.Contains(".db") || connectionString.Contains("Data Source"))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

// 2. Cryptography & Security Services
string codePepper = builder.Configuration["Security:CodePepper"] ?? "JNNTool_Pepper_ActivationCode_Secret_Salt_2026!";
string jwtSecret = builder.Configuration["Security:JwtSecret"] ?? "JNNTool_SuperSecret_Jwt_SigningKey_AtLeast32BytesLong_2026!";
string jwtIssuer = builder.Configuration["Security:JwtIssuer"] ?? "LicenseManagementApi";
string jwtAudience = builder.Configuration["Security:JwtAudience"] ?? "JNNToolRevitClient";

builder.Services.AddSingleton<ICodeHasher>(new CodeHasher(codePepper));
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator>(new JwtTokenGenerator(jwtSecret, jwtIssuer, jwtAudience));

// 3. Domain & Application Services
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// 4. JWT Authentication
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in strict production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 5. Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 6. CORS for Admin Dashboard
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Seed initial database / admin if necessary
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (useInMemoryDb || connectionString.Contains(".db") || connectionString.Contains("Data Source"))
    {
        db.Database.EnsureCreated();
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""RegistrationRequests"" (
                    ""Id"" TEXT NOT NULL PRIMARY KEY,
                    ""Email"" TEXT NOT NULL,
                    ""NormalizedEmail"" TEXT NOT NULL,
                    ""FullName"" TEXT NULL,
                    ""PhoneNumber"" TEXT NULL,
                    ""MachineId"" TEXT NULL,
                    ""DeviceName"" TEXT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    ""Status"" TEXT NOT NULL,
                    ""ApprovedActivationCode"" TEXT NULL,
                    ""ProcessedAtUtc"" TEXT NULL
                );
            ");
        }
        catch { }
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN ""FullName"" TEXT;"); } catch { }
        try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN ""PhoneNumber"" TEXT;"); } catch { }
    }

    if (!db.AdminUsers.Any())
    {
        string defaultAdminEmail = "admin@jnntool.com";
        var defaultAdmin = new AdminUser
        {
            Email = defaultAdminEmail,
            NormalizedEmail = defaultAdminEmail.ToLowerInvariant(),
            PasswordHash = passwordHasher.HashPassword("Admin@JNNTool2026!"),
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.AdminUsers.Add(defaultAdmin);
        db.SaveChanges();
    }
}

// HTTP Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program public for WebApplicationFactory in integration tests
public partial class Program { }
