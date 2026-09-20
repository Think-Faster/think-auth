using AuthService.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Common;
using WebAPI.Services;

namespace WebAPI.Controllers
{
	[ApiController]
	public class UserController : ControllerBase
	{
		private readonly AuthContext _context;
		private readonly IAuthSessionService _authSessionService;
		private readonly ILogger<UserController> _logger;

		public UserController(
			AuthContext context,
			IAuthSessionService authSessionService,
			ILogger<UserController> logger)
		{
			_context = context;
			_authSessionService = authSessionService;
			_logger = logger;
		}

		// [AllowAnonymous], т.к. авторизацию делаем вручную через
		// AuthSessionService (нужно самим решать, пробовать ли refresh,
		// а не отдавать 401 сразу на просроченном access-токене).
		[HttpGet("me")]
		[AllowAnonymous]
		public async Task<IActionResult> Me(CancellationToken cancellationToken)
		{
			var result = await _authSessionService.GetCurrentUserAsync(HttpContext, cancellationToken);

			if (!result.Success || result.User is null)
			{
				_logger.LogInformation(
					"/me: unauthenticated request ({Reason}).",
					result.FailureReason);

				AuthCookies.ClearAll(Response);

				return Unauthorized(new
				{
					message = "Не авторизован."
				});
			}

			return Ok(new
			{
				id = result.User.Id,
				userName = result.User.UserName,
				email = result.User.Email
			});
		}

		// [AllowAnonymous] по той же причине, что и у /me: сами решаем,
		// пробовать ли refresh вызывающего, вместо немедленного 401 от
		// стандартного пайплайна на просроченном access-токене.
		[HttpGet("users/{id:guid}")]
		[AllowAnonymous]
		public async Task<IActionResult> GetById(
			Guid id,
			CancellationToken cancellationToken)
		{
			var caller = await _authSessionService.GetCurrentUserAsync(HttpContext, cancellationToken);

			if (!caller.Success || caller.User is null)
			{
				_logger.LogInformation(
					"/users/{Id}: unauthenticated request ({Reason}).",
					id,
					caller.FailureReason);

				AuthCookies.ClearAll(Response);

				return Unauthorized(new
				{
					message = "Не авторизован."
				});
			}

			var user = await _context.Users.FindAsync(
				new object[] { id },
				cancellationToken);

			if (user is null)
			{
				return NotFound(new
				{
					message = "Пользователь не найден."
				});
			}

			return Ok(new
			{
				id = user.Id,
				userName = user.UserName,
				email = user.Email
			});
		}
	}
}