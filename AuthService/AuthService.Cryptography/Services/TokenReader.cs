using Microsoft.AspNetCore.Http;

namespace AuthService.Cryptography.Services
{
	public static class TokenReader
	{
		public static string? GetAccessToken(HttpContext context)
		{
			var header = context.Request.Headers["Authorization"].FirstOrDefault();
			if (!string.IsNullOrEmpty(header) && header.StartsWith("Bearer "))
				return header["Bearer ".Length..];

			return context.Request.Cookies["accessToken"];
		}

		public static string? GetRefreshToken(HttpContext context)
			=> context.Request.Cookies["refreshToken"];
	}
}
