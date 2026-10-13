using System;
using System.IO;
using System.Linq;
using LicenseManagement.Application.Interfaces;
using LicenseManagement.Domain.Entities;
using LicenseManagement.Domain.Enums;
using LicenseManagement.Infrastructure.Data;
using LicenseManagement.Infrastructure.Security;
using LicenseManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddSingleton<ICodeHasher>(new CodeHasher(codePepper));
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenGenerator>(new JwtTokenGenerator(jwtSecret));

builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// 3. Cookie Authentication for Web Admin Dashboard
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.Cookie.Name = ".JNNTool.AdminSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

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
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
