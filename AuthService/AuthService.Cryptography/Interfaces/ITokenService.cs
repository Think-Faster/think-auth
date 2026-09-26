using System.Security.Claims;

namespace AuthService.Cryptography.Interfaces
{
	public interface ITokenService
	{
		string GenerateAccessToken(string subject);

		string GenerateToken(
			string subject,
			int minutes,
			string tokenType = "access");

		string GenerateRefreshToken(string subject);

		ClaimsPrincipal? ValidateAccessToken(
			string token,
			bool validateLifetime = true);

		ClaimsPrincipal? ValidateRefreshToken(
			string token);

		string GetPublicKey();
	}
}