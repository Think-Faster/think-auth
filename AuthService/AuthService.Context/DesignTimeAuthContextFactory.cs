using AuthService.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

public class DesignTimeAuthContextFactory 
	: IDesignTimeDbContextFactory<AuthContext>
{
	public AuthContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<AuthContext>();

		// Пытаемся получить строку подключения из разных источников
		var connectionString = GetConnectionString(args);

		if (string.IsNullOrEmpty(connectionString))
		{
			// Для Add-Migration используем заглушку (БД не требуется)
			connectionString = "Host=localhost;Database=DesignTimeDb;";
		}

		optionsBuilder.UseNpgsql(
			connectionString,
			npgsqlOptions =>
			{
				npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
			}
		);

		return new AuthContext(optionsBuilder.Options);
	}

	private string GetConnectionString(string[] args)
	{
		if (args?.Length > 0 && !string.IsNullOrEmpty(args[0]))
		{
			return args[0];
		}

		try
		{
			var configuration = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: true)
				.AddEnvironmentVariables()
				.Build();

			var connString = configuration.GetConnectionString("DefaultConnection");
			if (!string.IsNullOrEmpty(connString))
				return connString;
		}
		catch 
		{ 
		}

		var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
		if (!string.IsNullOrEmpty(envConn))
			return envConn;

		return null;
	}
}