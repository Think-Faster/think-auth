using StackExchange.Redis;

namespace WebAPI.Services.RateLimit;

public class RedisRateLimitService : IRateLimitService
{
	private const int MaxAttempts = 5;
	private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

	private readonly IConnectionMultiplexer _redis;
	private readonly ILogger<RedisRateLimitService> _logger;

	public RedisRateLimitService(
		IConnectionMultiplexer redis,
		ILogger<RedisRateLimitService> logger)
	{
		_redis = redis;
		_logger = logger;
	}


	public async Task<RateLimitResult> CheckLoginAsync(
		string userName,
		string ipAddress,
		CancellationToken cancellationToken = default)
	{
		var database = _redis.GetDatabase();

		var key = BuildKey(
			userName,
			ipAddress);

		var attempts = await database.StringGetAsync(key);

		if (!attempts.HasValue)
		{
			return new RateLimitResult(
				Allowed: true,
				Attempts: 0,
				MaxAttempts: MaxAttempts,
				RetryAfterSeconds: 0);
		}

		if (!int.TryParse(attempts.ToString(), out var attemptCount))
		{
			_logger.LogWarning(
				"Invalid rate limit value in Redis for key {Key}. Resetting.",
				key);

			await database.KeyDeleteAsync(key);

			return new RateLimitResult(
				Allowed: true,
				Attempts: 0,
				MaxAttempts: MaxAttempts,
				RetryAfterSeconds: 0);
		}

		if (attemptCount < MaxAttempts)
		{
			return new RateLimitResult(
				Allowed: true,
				Attempts: attemptCount,
				MaxAttempts: MaxAttempts,
				RetryAfterSeconds: 0);
		}

		var ttl = await database.KeyTimeToLiveAsync(key);

		var retryAfterSeconds = ttl.HasValue
			? Math.Max(
				1,
				(int)Math.Ceiling(ttl.Value.TotalSeconds))
			: 60;

		return new RateLimitResult(
			Allowed: false,
			Attempts: attemptCount,
			MaxAttempts: MaxAttempts,
			RetryAfterSeconds: retryAfterSeconds);
	}


	public async Task RegisterFailedLoginAsync(
		string userName,
		string ipAddress,
		CancellationToken cancellationToken = default)
	{
		var database = _redis.GetDatabase();

		var key = BuildKey(
			userName,
			ipAddress);

		var attempts =
			await database.StringIncrementAsync(key);

		if (attempts == 1)
		{
			await database.KeyExpireAsync(
				key,
				Window);
		}

		_logger.LogWarning(
			"Failed login attempt. UserName: {UserName}, IP: {IpAddress}, Attempts: {Attempts}/{MaxAttempts}.",
			userName,
			ipAddress,
			attempts,
			MaxAttempts);
	}


	public async Task ResetLoginAsync(
		string userName,
		string ipAddress,
		CancellationToken cancellationToken = default)
	{
		var database = _redis.GetDatabase();

		var key = BuildKey(
			userName,
			ipAddress);

		await database.KeyDeleteAsync(key);

		_logger.LogInformation(
			"Login rate limit reset for UserName: {UserName}, IP: {IpAddress}.",
			userName,
			ipAddress);
	}


	private static string BuildKey(
		string userName,
		string ipAddress)
	{
		var normalizedUserName =
			userName.Trim().ToLowerInvariant();

		return $"auth:login-rate:{ipAddress}:{normalizedUserName}";
	}
}