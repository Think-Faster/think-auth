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

		public AuthController(
			AuthContext context,
			IPasswordHasher passwordHasher,
			ITokenService tokenService)
		{
			_context = context;
			_passwordHasher = passwordHasher;
			_tokenService = tokenService;
		}

		[HttpPost("register")]
		[AllowAnonymous]
		public async Task<IActionResult> Register(
			[FromBody] RegisterRequest request,
			CancellationToken cancellationToken)
		{
			var userNameExists = await _context.Users
				.AnyAsync(
					x => x.UserName == request.UserName,
					cancellationToken);

			if (userNameExists)
			{
				return Conflict(new
				{
					message = "Пользователь с таким логином уже существует."
				});
			}

			var emailExists = await _context.Users
				.AnyAsync(
					x => x.Email == request.Email,
					cancellationToken);

			if (emailExists)
			{
				return Conflict(new
				{
					message = "Пользователь с таким email уже существует."
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

			var accessToken = _tokenService.GenerateAccessToken(
				user.Id.ToString());

			SetAccessTokenCookie(accessToken);

			return Ok(new
			{
				user = new
				{
					id = user.Id,
					userName = user.UserName,
					email = user.Email,
					superUser = user.SuperUser
				}
			});
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
