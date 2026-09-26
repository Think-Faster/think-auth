using AuthService.Cryptography.Interfaces;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

public class Argon2PasswordHasher : IPasswordHasher
{
	public string Hash(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(16);

		var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
		{
			Salt = salt,
			DegreeOfParallelism = 4,
			MemorySize = 65536, // 64MB
			Iterations = 3
		};

		var hash = argon2.GetBytes(32);

		return $"v1.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
	}

	public bool Verify(string password, string storedHash)
	{
		var parts = storedHash.Split('.');
		if (parts.Length != 3)
			return false;

		var salt = Convert.FromBase64String(parts[1]);
		var hash = Convert.FromBase64String(parts[2]);

		var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
		{
			Salt = salt,
			DegreeOfParallelism = 4,
			MemorySize = 65536,
			Iterations = 3
		};

		var computed = argon2.GetBytes(32);

		return CryptographicOperations.FixedTimeEquals(hash, computed);
	}
}