using AuthService.Context;
using AuthService.Cryptography.Interfaces;
using AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Common;
using WebAPI.Contracts;
using WebAPI.Services;
using WebAPI.Services.Audit;
using WebAPI.Services.RateLimit;

namespace WebAPI.Controllers
{
	[ApiController]
	public class AuthController : ControllerBase
	{
		private readonly AuthContext _context;
		private readonly IPasswordHasher _passwordHasher;
		private readonly ITokenService _tokenService;
		private readonly IAuthSessionService _authSessionService;
		private readonly ILogger<AuthController> _logger;
		private readonly IRateLimitService _rateLimitService;
		private readonly AuditWriter _audit;

		public AuthController(
			AuthContext context,
			IPasswordHasher passwordHasher,
			ITokenService tokenService,
			IAuthSessionService authSessionService,
			ILogger<AuthController> logger,
			IRateLimitService rateLimitService,
			AuditWriter audit)
		{
			_context = context;
			_passwordHasher = passwordHasher;
			_tokenService = tokenService;
			_authSessionService = authSessionService;
			_logger = logger;
			_rateLimitService = rateLimitService;
			_audit = audit;
		}

		[HttpPost("register")]
		[AllowAnonymous]
		public async Task<IActionResult> Register(
		[FromBody] RegisterRequest request,
		CancellationToken cancellationToken)
		{
			_logger.LogInformation(
				"Registration attempt for username {UserName}, email {Email}.",
				request.UserName,
				request.Email);

			try
			{
				var userNameExists =
					await _context.Users
						.AnyAsync(
							x => x.UserName == request.UserName,
							cancellationToken);

				if (userNameExists)
				{
					_logger.LogWarning(
						"Registration rejected: username {UserName} already exists.",
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
						"Registration rejected: email {Email} already exists.",
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

				await _audit.WriteAsync("user.created", "success", user.Id, user.UserName,
					new Dictionary<string, object?> { ["method"] = "register" });

				IssueTokens(user);

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
					"Registration failed for username {UserName}.",
					request.UserName);

				throw;
			}
		}

		[HttpPost("login")]
		[AllowAnonymous]
		public async Task<IActionResult> Login(
		[FromBody] LoginRequest request,
		CancellationToken cancellationToken)
		{
			var userName = request.UserName.Trim();

			var ipAddress =
				HttpContext.Connection.RemoteIpAddress?
					.ToString()
				?? "unknown";

			_logger.LogInformation(
				"Login attempt for username {UserName} from IP {IpAddress}.",
				userName,
				ipAddress);

			var rateLimit =
				await _rateLimitService.CheckLoginAsync(
					userName,
					ipAddress,
					cancellationToken);

			if (!rateLimit.Allowed)
			{
				_logger.LogWarning(
					"Login rate limit exceeded for username {UserName} from IP {IpAddress}. Retry after {RetryAfter} seconds.",
					userName,
					ipAddress,
					rateLimit.RetryAfterSeconds);

				await _audit.WriteAsync("account.locked", "denied", null, userName,
					new Dictionary<string, object?> { ["retry_after_seconds"] = rateLimit.RetryAfterSeconds });

				Response.Headers.RetryAfter =
					rateLimit.RetryAfterSeconds.ToString();

				return StatusCode(
					StatusCodes.Status429TooManyRequests,
					new
					{
						message =
							"Слишком много неудачных попыток входа. Попробуйте позже.",
						retryAfterSeconds =
							rateLimit.RetryAfterSeconds
					});
			}

			try
			{
				var user =
					await _context.Users
						.SingleOrDefaultAsync(
							x => x.UserName == userName,
							cancellationToken);

				if (user == null ||
					string.IsNullOrEmpty(user.PasswordHash) ||
					!_passwordHasher.Verify(
						request.Password,
						user.PasswordHash))
				{
					await _rateLimitService.RegisterFailedLoginAsync(
						userName,
						ipAddress,
						cancellationToken);

					_logger.LogWarning(
						"Invalid login credentials for username {UserName} from IP {IpAddress}.",
						userName,
						ipAddress);

					// Логин — как его ввели; пароль не пишется никогда (§6.3).
					await _audit.WriteAsync("login.failure", "denied", user?.Id, userName,
						new Dictionary<string, object?>
						{
							["reason"] = user == null ? "unknown_login" : "wrong_password"
						});

					return Unauthorized(new
					{
						message =
							"Неверный логин или пароль."
					});
				}

				await _rateLimitService.ResetLoginAsync(
					userName,
					ipAddress,
					cancellationToken);

				IssueTokens(user);

				_logger.LogInformation(
					"User {UserId} successfully logged in from IP {IpAddress}.",
					user.Id,
					ipAddress);

				await _audit.WriteAsync("login.success", "success", user.Id, user.UserName,
					new Dictionary<string, object?> { ["method"] = "password" });

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
					"Login failed unexpectedly for username {UserName} from IP {IpAddress}.",
					userName,
					ipAddress);

				throw;
			}
		}

		[HttpPost("refresh")]
		[AllowAnonymous]
		public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
		{
			var result = await _authSessionService.RefreshAsync(HttpContext, cancellationToken);

			if (!result.Success || result.User is null)
			{
				_logger.LogWarning("Refresh rejected: {Reason}.", result.FailureReason);

				AuthCookies.ClearAll(Response);

				return Unauthorized(new
				{
					message = "Сессия истекла, требуется повторный вход."
				});
			}

			_logger.LogInformation(
				"Session refreshed for user {UserId}.",
				result.User.Id);

			await _audit.WriteAsync("token.refreshed", "success", result.User.Id, result.User.UserName);

			return Ok(new
			{
				id = result.User.Id,
				userName = result.User.UserName,
				email = result.User.Email
			});
		}

		private void IssueTokens(User user)
		{
			var accessToken = _tokenService.GenerateAccessToken(user.Id.ToString(), user.UserName);
			var refreshToken = _tokenService.GenerateRefreshToken(user.Id.ToString(), user.UserName);

			AuthCookies.SetAccessToken(Response, accessToken);
			AuthCookies.SetRefreshToken(Response, refreshToken);

			_logger.LogInformation(
				"Access и refresh токены выпущены для пользователя {UserId}.",
				user.Id);
		}
	}
}