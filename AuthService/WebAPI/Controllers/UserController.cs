using AuthService.Context;
using AuthService.Cryptography.Interfaces;
using AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Common;
using WebAPI.Contracts;
using WebAPI.Services;

namespace WebAPI.Controllers
{
	[ApiController]
	public class UserController : ControllerBase
	{
		private readonly AuthContext _context;
		private readonly IAuthSessionService _authSessionService;
		private readonly ILogger<UserController> _logger;
		private readonly IPasswordHasher _passwordHasher;

		public UserController(
			AuthContext context,
			IAuthSessionService authSessionService,
			ILogger<UserController> logger,
			IPasswordHasher passwordHasher)
		{
			_context = context;
			_authSessionService = authSessionService;
			_logger = logger;
			_passwordHasher = passwordHasher;
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

		[HttpPost("create")]
		[AllowAnonymous]
		public async Task<IActionResult> Register(
		[FromBody] RegisterRequest request,
		CancellationToken cancellationToken)
		{
			_logger.LogInformation(
				"Creating user attempt for username {UserName}, email {Email}.",
				request.UserName,
				request.Email);

			try
			{
				var result = await _authSessionService.GetCurrentUserAsync(HttpContext, cancellationToken);

				if (!result.Success || result.User is null)
				{
					_logger.LogInformation(
						"/create: unauthenticated request ({Reason}).",
						result.FailureReason);

					AuthCookies.ClearAll(Response);

					return Unauthorized(new
					{
						message = "Не авторизован."
					});
				}

				if (!result.User.SuperUser)
				{
					_logger.LogInformation(
						"/create: forbid request ({Reason}).",
						result.FailureReason);

					return Unauthorized(new
					{
						message = "Недостаточно прав."
					});
				}

				var userNameExists =
					await _context.Users
						.AnyAsync(
							x => x.UserName == request.UserName,
							cancellationToken);

				if (userNameExists)
				{
					_logger.LogWarning(
						"Creating user rejected: username {UserName} already exists.",
						request.UserName);

					return Conflict(new
					{
						message =
							"Пользователь с таким логином уже существует."
					});
				}

				var emailExists =
					await _context.Users
						.AnyAsync(
							x => x.Email == request.Email,
							cancellationToken);

				if (emailExists)
				{
					_logger.LogWarning(
						"Creating user rejected: email {Email} already exists.",
						request.Email);

					return Conflict(new
					{
						message =
							"Пользователь с таким email уже существует."
					});
				}

				var user = new User
				{
					Id = Guid.NewGuid(),
					UserName = request.UserName,
					Email = request.Email,
					SuperUser = false,
					CreatedBy = Guid.Empty,
					UpdatedBy = Guid.Empty,
					CreatedAt = DateTimeOffset.UtcNow,
					UpdatedAt = DateTimeOffset.UtcNow
				};

				user.SetPassword(
					request.Password,
					_passwordHasher);

				_context.Users.Add(user);

				await _context.SaveChangesAsync(cancellationToken);

				_logger.LogInformation(
					"User {UserId} successfully created.",
					user.Id);

				return Ok(new
				{
					id = user.Id,
					userName = user.UserName,
					email = user.Email
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Creating user failed for username {UserName}.",
					request.UserName);

				throw;
			}
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