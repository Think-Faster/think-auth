using StackExchange.Redis;

namespace WebAPI.Services
{
	public static class AddRedisExtension
	{
		public static WebApplicationBuilder AddRedis(this WebApplicationBuilder builder)
		{
			var redisConnectionString =
				builder.Configuration["Redis:ConnectionString"];

			if (string.IsNullOrWhiteSpace(redisConnectionString))
			{
				throw new InvalidOperationException(
					"Redis:ConnectionString is not configured.");
			}

			builder.Services.AddSingleton<IConnectionMultiplexer>(
				_ =>
				{
					var configuration =
						ConfigurationOptions.Parse(
							redisConnectionString);

					configuration.AbortOnConnectFail = false;

					return ConnectionMultiplexer.Connect(
						configuration);
				});

			builder.Services.AddSingleton<IRateLimitService, RedisRateLimitService>();

			return builder;
		}
	}
}
