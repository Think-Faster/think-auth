using System.Security.Claims;

namespace AuthService.Cryptography.Interfaces
{
	public interface ITokenService
	{
		string GenerateAccessToken(string subject, string? login = null);

		string GenerateToken(
			string subject,
			int minutes,
			string tokenType = "access",
			string? login = null);

		string GenerateRefreshToken(string subject, string? login = null);

		ClaimsPrincipal? ValidateAccessToken(
			string token,
			bool validateLifetime = true);

		ClaimsPrincipal? ValidateRefreshToken(
			string token);

		string GetPublicKey();
	}
}