using AuthService.Cryptography.Interfaces;
using Newtonsoft.Json;

namespace AuthService.Models
{
	/// <summary>
	/// Модель данных пользователя.
	/// </summary>
	public class User
		: EntityBase
	{
		[JsonProperty("userName")]
		public string? UserName { get; set; }

		[JsonProperty("email")]
		public string? Email { get; set; }

		public bool SuperUser { get; set; }

		[JsonIgnore]
		public string? PasswordHash { get; private set; }

		public void SetPassword(string password, IPasswordHasher hasher)
		{
			PasswordHash = hasher.Hash(password);
		}

		public bool VerifyPassword(string password, IPasswordHasher hasher)
		{
			return hasher.Verify(password, PasswordHash!);
		}
	}
}
