using AuthService.Cryptography.Interfaces;
using AuthService.Cryptography.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Cryptography
{
	public static class Extension
	{
		public static IServiceCollection AddCryptography(this IServiceCollection services)
		{
			services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
			return services;
		}

		public static IServiceCollection AddTokenService(this IServiceCollection services)
		{
			services.AddSingleton<RsaKeyProvider>();
			services.AddScoped<ITokenService, TokenService>();

			return services;
		}
	}
}
