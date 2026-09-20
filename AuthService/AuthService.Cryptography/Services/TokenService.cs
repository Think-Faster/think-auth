using AuthService.Cryptography.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AuthService.Cryptography.Services;

public class TokenService : ITokenService
{
	private readonly RSA _rsa;
	private readonly RsaSecurityKey _privateKey;
	private readonly string _issuer;
	private readonly string _audience;

	public TokenService(
		IConfiguration configuration,
		RsaKeyProvider rsaKeyProvider)
	{
		_issuer = configuration["Jwt:Issuer"]
			?? "auth-service";

		_audience = configuration["Jwt:Audience"]
			?? "api";

		_rsa = rsaKeyProvider.LoadOrCreate();

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


	public string GenerateToken(
		string subject,
		int minutes)
	{
		var claims = new[]
		{
			new Claim(
				ClaimTypes.NameIdentifier,
				subject),

			new Claim(
				JwtRegisteredClaimNames.Sub,
				subject)
		};

		var credentials = new SigningCredentials(
			_privateKey,
			SecurityAlgorithms.RsaSha256);

		var token = new JwtSecurityToken(
			issuer: _issuer,
			audience: _audience,
			claims: claims,
			expires: DateTime.UtcNow.AddMinutes(minutes),
			signingCredentials: credentials);

		return new JwtSecurityTokenHandler()
			.WriteToken(token);
	}


	public ClaimsPrincipal? ValidateAccessToken(
		string token,
		bool validateLifetime = true)
	{
		return Validate(
			token,
			validateLifetime);
	}


	public ClaimsPrincipal? ValidateRefreshToken(
		string token)
	{
		return Validate(
			token,
			true);
	}


	private ClaimsPrincipal? Validate(
		string token,
		bool validateLifetime)
	{
		var handler = new JwtSecurityTokenHandler();

		try
		{
			return handler.ValidateToken(
				token,
				new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidIssuer = _issuer,

					ValidateAudience = true,
					ValidAudience = _audience,

					ValidateLifetime = validateLifetime,

					ValidateIssuerSigningKey = true,
					IssuerSigningKey = _privateKey,

					ClockSkew = TimeSpan.Zero
				},
				out _);
		}
		catch
		{
			return null;
		}
	}


	public string GetPublicKey()
	{
		var publicKey = _rsa.ExportSubjectPublicKeyInfo();

		var base64 = Convert.ToBase64String(publicKey);

		var builder = new StringBuilder();

		builder.AppendLine("-----BEGIN PUBLIC KEY-----");

		for (var i = 0; i < base64.Length; i += 64)
		{
			builder.AppendLine(
				base64.Substring(
					i,
					Math.Min(
						64,
						base64.Length - i)));
		}

		builder.AppendLine("-----END PUBLIC KEY-----");

		return builder.ToString();
	}


	public string GenerateServiceToken(
		ClaimsPrincipal client)
	{
		var claims = new[]
		{
			new Claim(
				ClaimTypes.NameIdentifier,
				client.FindFirst(
					ClaimTypes.NameIdentifier)?.Value
				?? string.Empty),

			new Claim(
				"client_type",
				"machine")
		};

		var credentials = new SigningCredentials(
			_privateKey,
			SecurityAlgorithms.RsaSha256);

		var token = new JwtSecurityToken(
			issuer: _issuer,
			audience: "mail-service",
			claims: claims,
			expires: DateTime.UtcNow.AddDays(1),
			signingCredentials: credentials);

		return new JwtSecurityTokenHandler()
			.WriteToken(token);
	}
}