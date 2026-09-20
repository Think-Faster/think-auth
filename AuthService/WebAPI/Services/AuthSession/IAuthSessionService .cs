using AuthService.Models;
using Microsoft.AspNetCore.Http;

namespace WebAPI.Services
{
	public class AuthSessionResult
	{
		public bool Success { get; init; }
		public User? User { get; init; }
		public string? FailureReason { get; init; }

		public static AuthSessionResult Fail(string reason) =>
			new() { Success = false, FailureReason = reason };

		public static AuthSessionResult Ok(User user) =>
			new() { Success = true, User = user };
	}

	public interface IAuthSessionService
	{
		/// <summary>
		/// Текущий пользователь по access-токену из cookie. Если access
		/// просрочен или отсутствует, но refresh-токен валиден — прозрачно
		/// перевыпускает пару токенов и обновляет cookie.
		/// </summary>
		Task<AuthSessionResult> GetCurrentUserAsync(
			HttpContext httpContext,
			CancellationToken cancellationToken);

		/// <summary>
		/// Явный refresh: требует валидный refresh-токен. Если рядом есть
		/// access-токен (пусть и просроченный) — проверяет, что оба токена
		/// принадлежат одному пользователю. Перевыпускает обе пары.
		/// </summary>
		Task<AuthSessionResult> RefreshAsync(
			HttpContext httpContext,
			CancellationToken cancellationToken);
	}
}