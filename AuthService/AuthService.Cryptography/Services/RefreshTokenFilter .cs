using AuthService.Cryptography.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AuthService.Cryptography.Services
{
	public class RefreshTokenFilter : IAsyncResultFilter
	{
		private readonly ITokenService _tokenService;

		public RefreshTokenFilter(ITokenService tokenService)
		{
			_tokenService = tokenService;
		}

		public async Task OnResultExecutionAsync(
			ResultExecutingContext context,
			ResultExecutionDelegate next)
		{
			await next();

			var http = context.HttpContext;

			var accessExpired = http.Items.ContainsKey("access_expired");

			if (!accessExpired)
				return;

			var refreshToken = TokenReader.GetRefreshToken(http);
			if (string.IsNullOrEmpty(refreshToken))
				return;

			var principal = _tokenService.ValidateRefreshToken(refreshToken);
			if (principal == null)
				return;

			var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			if (userId == null)
				return;

			var newAccess = _tokenService.GenerateAccessToken(userId);
			var newRefresh = _tokenService.GenerateRefreshToken(userId);

			http.Response.Cookies.Append("accessToken", newAccess, new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Strict
			});

			http.Response.Cookies.Append("refreshToken", newRefresh, new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Strict
			});
		}
	}
}
