using Microsoft.AspNetCore.Http;

namespace WebAPI.Common
{
	public static class AuthCookies
	{
		public const string AccessTokenCookieName = "access_token";
		public const string RefreshTokenCookieName = "refresh_token";

		// Должно соответствовать срокам жизни, зашитым в TokenService
		// (GenerateAccessToken / GenerateRefreshToken). Если поменяете
		// минуты там — поправьте и здесь.
		private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(10);
		private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromHours(24);

		public static void SetAccessToken(HttpResponse response, string token)
		{
			response.Cookies.Append(
				AccessTokenCookieName,
				token,
				new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					SameSite = SameSiteMode.Lax,
					MaxAge = AccessTokenLifetime,
					Path = "/",
					IsEssential = true
				});
		}

		public static void SetRefreshToken(HttpResponse response, string token)
		{
			response.Cookies.Append(
				RefreshTokenCookieName,
				token,
				new CookieOptions
				{
					HttpOnly = true,
					Secure = true,
					// Refresh-токен чувствительнее access — Strict вместо Lax.
					// Если фронт и бэк не на одном site (в терминах SameSite),
					// придётся ослабить до Lax.
					SameSite = SameSiteMode.Strict,
					MaxAge = RefreshTokenLifetime,
					Path = "/",
					IsEssential = true
				});
		}

		public static void ClearAll(HttpResponse response)
		{
			response.Cookies.Delete(AccessTokenCookieName, new CookieOptions { Path = "/" });
			response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/" });
		}
	}
}