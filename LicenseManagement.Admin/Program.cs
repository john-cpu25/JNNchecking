using System;
using System.IO;
using System.Linq;
using System.Text;
using LicenseManagement.Application.Interfaces;
using LicenseManagement.Domain.Entities;
using LicenseManagement.Domain.Enums;
using LicenseManagement.Infrastructure.Data;
using LicenseManagement.Infrastructure.Security;
using LicenseManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Cloud Port Binding (Render, Railway, Fly.io provide PORT environment variable)
string? envPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(envPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{envPort}");
}

// 1. Database Configuration: Shared SQLite DB at server/licenses.db
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=licenses.db";

string rawDbPath = connectionString.Replace("Data Source=", "").Trim();
if (Path.IsPathRooted(rawDbPath))
{
    string? dir = Path.GetDirectoryName(rawDbPath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
        try { Directory.CreateDirectory(dir); } catch { }
    }
}
else if (connectionString.Contains("licenses.db"))
{
    string sharedDb = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "licenses.db"));
    connectionString = $"Data Source={sharedDb}";
}

bool useInMemoryDb = builder.Configuration.GetValue<bool>("UseInMemoryDb") ||
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

// 2. Security & Application Services
string codePepper = builder.Configuration["Security:CodePepper"] ?? "JNNTool_Pepper_ActivationCode_Secret_Salt_2026!";
string jwtSecret = builder.Configuration["Security:JwtSecret"] ?? "JNNTool_SuperSecret_Jwt_SigningKey_AtLeast32BytesLong_2026!";
string jwtIssuer = builder.Configuration["Security:JwtIssuer"] ?? "LicenseManagementApi";
string jwtAudience = builder.Configuration["Security:JwtAudience"] ?? "JNNToolRevitClient";

builder.Services.AddSingleton<ICodeHasher>(new CodeHasher(codePepper));
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator>(new JwtTokenGenerator(jwtSecret, jwtIssuer, jwtAudience));

builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// 3. Dual Authentication: Cookie (Web Admin) + JWT (Revit API)
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.Cookie.Name = ".JNNTool.AdminSession";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = false;
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

// 4. CORS & API Controllers for Revit Add-in
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

// Seed initial admin user if empty
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

    // Seed JNNTool Admin
    string jnnAdminEmail = "admin@jnntool.com";
    if (!db.AdminUsers.Any(u => u.NormalizedEmail == jnnAdminEmail))
    {
        var jnnAdmin = new AdminUser
        {
            Email = jnnAdminEmail,
            NormalizedEmail = jnnAdminEmail.ToLowerInvariant(),
            PasswordHash = passwordHasher.HashPassword("Admin@JNNTool2026!"),
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.AdminUsers.Add(jnnAdmin);
    }

    // Seed legacy Admin for backward compatibility
    string legacyAdminEmail = "admin@nhanstr.com";
    if (!db.AdminUsers.Any(u => u.NormalizedEmail == legacyAdminEmail))
    {
        var legacyAdmin = new AdminUser
        {
            Email = legacyAdminEmail,
            NormalizedEmail = legacyAdminEmail.ToLowerInvariant(),
            PasswordHash = passwordHasher.HashPassword("Admin@NhanSTR2026!"),
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.AdminUsers.Add(legacyAdmin);
    }

    db.SaveChanges();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
