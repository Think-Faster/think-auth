using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace AuthService.Cryptography.Services;

public class RsaKeyProvider
{
	private readonly string _privateKeyPath;

	public RsaKeyProvider(IConfiguration configuration)
	{
		_privateKeyPath = configuration["Jwt:PrivateKeyPath"]
			?? throw new InvalidOperationException(
				"Jwt:PrivateKeyPath is not configured.");
	}

	public RSA LoadOrCreate()
	{
		var directory = Path.GetDirectoryName(_privateKeyPath);

		if (string.IsNullOrWhiteSpace(directory))
		{
			throw new InvalidOperationException(
				"JWT private key directory is not configured.");
		}

		Directory.CreateDirectory(directory);

		if (File.Exists(_privateKeyPath))
		{
			var privateKeyPem = File.ReadAllText(_privateKeyPath);

			var existingRsa = RSA.Create();

			try
			{
				existingRsa.ImportFromPem(privateKeyPem.ToCharArray());

				return existingRsa;
			}
			catch
			{
				existingRsa.Dispose();

				throw new InvalidOperationException(
					"JWT private key exists but is invalid.");
			}
		}

		var rsa = RSA.Create(2048);

		var pem = rsa.ExportRSAPrivateKeyPem();

		File.WriteAllText(
			_privateKeyPath,
			pem);

		try
		{
			File.SetUnixFileMode(
				_privateKeyPath,
				UnixFileMode.UserRead |
				UnixFileMode.UserWrite);
		}
		catch
		{
			// Windows / environments without Unix file permissions.
		}

		return rsa;
	}
}