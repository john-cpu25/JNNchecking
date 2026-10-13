using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LicenseManagement.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace LicenseManagement.Infrastructure.Security
{
    public interface IJwtTokenGenerator
    {
        string GenerateClientToken(User user, License license, Device device, TimeSpan? lifetime = null);
        string GenerateAdminToken(AdminUser admin, TimeSpan? lifetime = null);
    }

    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;

        public JwtTokenGenerator(string secretKey, string issuer = "LicenseManagementApi", string audience = "JNNToolRevitClient")
        {
            _secretKey = string.IsNullOrWhiteSpace(secretKey)
                ? "JNNTool_SuperSecret_Jwt_SigningKey_AtLeast32BytesLong_2026!"
                : secretKey;
            _issuer = issuer;
            _audience = audience;
        }

        public string GenerateClientToken(User user, License license, Device device, TimeSpan? lifetime = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new("license_id", license.Id.ToString()),
                new("device_id", device.Id.ToString()),
                new("device_hash", device.DeviceIdentifierHash),
                new("license_type", license.LicenseType.ToString()),
                new("license_status", license.Status.ToString()),
                new(ClaimTypes.Role, "Client")
            };

            var expiresAt = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(24));
            // Token expiration cannot exceed actual license expiration
            if (expiresAt > license.ExpiresAtUtc)
            {
                expiresAt = license.ExpiresAtUtc;
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public string GenerateAdminToken(AdminUser admin, TimeSpan? lifetime = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, admin.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.Role, admin.Role.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(8)),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
