using Hokm.Application.Interfaces;
using Hokm.Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Hokm.Infrastructure.Security
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly InfrastructureSettings _settings;
        public JwtTokenGenerator(IOptions<InfrastructureSettings> settings)
        {
            _settings = settings.Value;
        }

        public string GenerateToken(Guid id, string phoneNumber, string userName)
        {
            return CreateToken(id.ToString(), phoneNumber, userName);
        }

        private string CreateToken(string userId, string phoneNumber, string userName)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim("UserName", userName),
                new Claim("PhoneNumber", phoneNumber ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_settings.JwtExpiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
