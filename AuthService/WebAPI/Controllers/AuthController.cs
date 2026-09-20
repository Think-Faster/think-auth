using AuthService.Context;
using AuthService.Cryptography.Interfaces;
using AuthService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Contracts;

namespace WebAPI.Controllers
{
	[ApiController]
	public class AuthController : ControllerBase
	{
		private const string AccessTokenCookieName = "access_token";

		private readonly AuthContext _context;
		private readonly IPasswordHasher _passwordHasher;
		private readonly ITokenService _tokenService;
		private readonly ILogger<AuthController> _logger;

		public AuthController(
			AuthContext context,
			IPasswordHasher passwordHasher,
			ITokenService tokenService,
			ILogger<AuthController> logger)
		{
			_context = context;
			_passwordHasher = passwordHasher;
			_tokenService = tokenService;
			_logger = logger;
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

				await _context.SaveChangesAsync(
					cancellationToken);


				_logger.LogInformation(
					"User {UserId} successfully created.",
					user.Id);


				var accessToken =
					_tokenService.GenerateAccessToken(
						user.Id.ToString());


				SetAccessTokenCookie(accessToken);


				_logger.LogInformation(
					"Access token generated for user {UserId}.",
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
					"Registration failed for username {UserName}.",
					request.UserName);

				throw;
			}
		}

		private void SetAccessTokenCookie(string token)
		{
			Response.Cookies.Append(
				AccessTokenCookieName,
				token,
				new CookieOptions
				{
					HttpOnly = true,

					Secure = true,

					SameSite = SameSiteMode.Lax,

					// JWT не должен жить дольше самого access token.
					// Здесь пример на 15 минут.
					MaxAge = TimeSpan.FromMinutes(15),

					Path = "/",

					IsEssential = true
				});
		}
	}
}
