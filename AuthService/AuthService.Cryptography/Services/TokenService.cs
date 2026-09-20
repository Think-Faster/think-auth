using AuthService.Cryptography.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;

namespace AuthService.Cryptography.Services
{
	public class TokenService : ITokenService
	{
		private readonly RSA _rsa;
		private readonly RsaSecurityKey _privateKey;

		public TokenService(IConfiguration configuration)
		{
			var privateKeyPath = configuration["Jwt:PrivateKeyPath"];

			if (string.IsNullOrEmpty(privateKeyPath))
				throw new Exception("Private key path is missing");

			var privateKeyPem = File.ReadAllText(privateKeyPath);

			_rsa = RSA.Create();
			_rsa.ImportFromPem(privateKeyPem.ToCharArray());
			_privateKey = new RsaSecurityKey(_rsa)
			{
				KeyId = "main-key"
			};
		}

		public string GenerateAccessToken(string subject)
		{
			return GenerateToken(subject, 10);
		}

		public string GenerateRefreshToken(string subject)
		{
			return GenerateToken(subject, 60 * 24);
		}

		public string GenerateToken(string subject, int minutes)
		{
			var claims = new[]
			{
				new Claim(ClaimTypes.NameIdentifier, subject),
				new Claim(JwtRegisteredClaimNames.Sub, subject),
				new Claim(JwtRegisteredClaimNames.Email, subject)
			};

			var creds = new SigningCredentials(
				new RsaSecurityKey(_rsa),
				SecurityAlgorithms.RsaSha256
			);

			var token = new JwtSecurityToken(
				issuer: "auth-service",
				audience: "api",
				claims: claims,
				expires: DateTime.UtcNow.AddMinutes(minutes),
				signingCredentials: creds
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}

		public ClaimsPrincipal? ValidateAccessToken(string token, bool validateLifetime = true)
		{
			return Validate(token, validateLifetime);
		}

		public ClaimsPrincipal? ValidateRefreshToken(string token)
		{
			return Validate(token, true);
		}

		private ClaimsPrincipal? Validate(string token, bool validateLifetime)
		{
			var handler = new JwtSecurityTokenHandler();

			try
			{
				return handler.ValidateToken(token, new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidIssuer = "auth-service",

					ValidateAudience = true,
					ValidAudience = "api",

					ValidateLifetime = validateLifetime,

					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new RsaSecurityKey(_rsa),

					ClockSkew = TimeSpan.Zero
				}, out _);
			}
			catch
			{
				return null;
			}
		}

		public string GetPublicKey()
		{
			return ExportPublicKeyPem(_rsa);
		}

		private string ExportPublicKeyPem(RSA rsa)
		{
			var publicKey = rsa.ExportSubjectPublicKeyInfo();
			var base64 = Convert.ToBase64String(publicKey);

			var builder = new StringBuilder();
			builder.AppendLine("-----BEGIN PUBLIC KEY-----");

			for (int i = 0; i < base64.Length; i += 64)
			{
				builder.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
			}

			builder.AppendLine("-----END PUBLIC KEY-----");

			return builder.ToString();
		}

		public string GenerateServiceToken(ClaimsPrincipal client)
		{
			var claims = new[]
			{
				new Claim(
					ClaimTypes.NameIdentifier,
					client.FindFirst(ClaimTypes.NameIdentifier)?.Value
				),

				new Claim("client_type", "machine")
			};

			var creds = new SigningCredentials(_privateKey, SecurityAlgorithms.RsaSha256);

			var token = new JwtSecurityToken(
				issuer: "auth-service",
				audience: "mail-service",
				claims: claims,
				expires: DateTime.UtcNow.AddDays(1),
				signingCredentials: creds
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}
	}
}
