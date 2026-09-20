using AuthService.Context;
using AuthService.Cryptography.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using WebAPI.Common;

namespace WebAPI.Services
{
	public class AuthSessionService : IAuthSessionService
	{
		private readonly AuthContext _context;
		private readonly ITokenService _tokenService;
		private readonly ILogger<AuthSessionService> _logger;

		public AuthSessionService(
			AuthContext context,
			ITokenService tokenService,
			ILogger<AuthSessionService> logger)
		{
			_context = context;
			_tokenService = tokenService;
			_logger = logger;
		}

		public async Task<AuthSessionResult> GetCurrentUserAsync(
			HttpContext httpContext,
			CancellationToken cancellationToken)
		{
			var accessToken = httpContext.Request.Cookies[AuthCookies.AccessTokenCookieName];

			if (!string.IsNullOrEmpty(accessToken))
			{
				var principal = _tokenService.ValidateAccessToken(accessToken);
				var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

				if (userId is not null && Guid.TryParse(userId, out var parsedId))
				{
					var user = await _context.Users.FindAsync(
						new object[] { parsedId },
						cancellationToken);

					if (user is not null)
					{
						return AuthSessionResult.Ok(user);
					}
				}
			}

			// Access-токена нет / просрочен / битый / пользователь не найден —
			// пробуем прозрачно поднять сессию через refresh-токен.
			return await RefreshAsync(httpContext, cancellationToken);
		}

		public async Task<AuthSessionResult> RefreshAsync(
			HttpContext httpContext,
			CancellationToken cancellationToken)
		{
			var refreshToken = httpContext.Request.Cookies[AuthCookies.RefreshTokenCookieName];

			if (string.IsNullOrEmpty(refreshToken))
			{
				return AuthSessionResult.Fail("refresh_token_missing");
			}

			var refreshPrincipal = _tokenService.ValidateRefreshToken(refreshToken);
			var refreshUserId = refreshPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

			if (refreshUserId is null || !Guid.TryParse(refreshUserId, out var refreshUserGuid))
			{
				_logger.LogWarning("Refresh отклонён: refresh-токен невалиден или просрочен.");
				return AuthSessionResult.Fail("refresh_token_invalid");
			}

			// Если рядом есть access-токен (пусть и просроченный), убеждаемся,
			// что оба токена выданы одному и тому же пользователю.
			var accessToken = httpContext.Request.Cookies[AuthCookies.AccessTokenCookieName];

			if (!string.IsNullOrEmpty(accessToken))
			{
				var accessPrincipal = _tokenService.ValidateAccessToken(
					accessToken,
					validateLifetime: false);

				var accessUserId = accessPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

				if (accessUserId is not null && accessUserId != refreshUserId)
				{
					_logger.LogWarning(
						"Refresh отклонён: access-токен принадлежит {AccessUserId}, а refresh-токен — {RefreshUserId}.",
						accessUserId,
						refreshUserId);

					return AuthSessionResult.Fail("token_mismatch");
				}
			}

			var user = await _context.Users.FindAsync(
				new object[] { refreshUserGuid },
				cancellationToken);

			if (user is null)
			{
				_logger.LogWarning(
					"Refresh отклонён: пользователь {UserId} из refresh-токена не найден.",
					refreshUserGuid);

				return AuthSessionResult.Fail("user_not_found");
			}

			var newAccessToken = _tokenService.GenerateAccessToken(user.Id.ToString());
			var newRefreshToken = _tokenService.GenerateRefreshToken(user.Id.ToString());

			AuthCookies.SetAccessToken(httpContext.Response, newAccessToken);
			AuthCookies.SetRefreshToken(httpContext.Response, newRefreshToken);

			_logger.LogInformation("Токены пользователя {UserId} обновлены.", user.Id);

			return AuthSessionResult.Ok(user);
		}
	}
}