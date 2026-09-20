using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Common;
using WebAPI.Services;

namespace WebAPI.Controllers
{
	[ApiController]
	public class UserController : ControllerBase
	{
		private readonly IAuthSessionService _authSessionService;
		private readonly ILogger<UserController> _logger;

		public UserController(
			IAuthSessionService authSessionService,
			ILogger<UserController> logger)
		{
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
	}
}