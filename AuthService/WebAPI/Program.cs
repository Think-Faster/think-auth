
using AuthService.Context;
using AuthService.Cryptography;
using AuthService.Cryptography.Interfaces;
using AuthService.Cryptography.Services;
using Microsoft.EntityFrameworkCore;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Init(args);

			// Add services to the container.
			builder.Logging.ClearProviders();

			builder.Logging.AddConsole();

			builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
			// Health checks
			builder.Services.AddHealthChecks();

			builder.Services.AddSwaggerGen();

            var app = builder.Build();

			app.UseRouting();

			// Health
			app.MapHealthChecks("/health");

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }

		public static WebApplicationBuilder Init(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			builder.Services
				.AddControllers(options =>
				{
					options.Filters.Add<RefreshTokenFilter>();
				});

			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();

			builder.Services
				.AddDbContext<AuthContext>(options =>
					options.UseNpgsql(
						builder.Configuration.GetConnectionString("DefaultConnection"),
						npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
							"__EFMigrationsHistory",
							"auth"))
				);

			builder.Services.AddCryptography();

			builder.Services.AddTokenService();
			builder.Services.AddScoped<RefreshTokenFilter>();

			return builder;
		}
	}
}
