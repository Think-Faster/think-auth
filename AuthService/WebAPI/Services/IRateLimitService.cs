namespace WebAPI.Services;

public interface IRateLimitService
{
	Task<RateLimitResult> CheckLoginAsync(
		string userName,
		string ipAddress,
		CancellationToken cancellationToken = default);

	Task RegisterFailedLoginAsync(
		string userName,
		string ipAddress,
		CancellationToken cancellationToken = default);

	Task ResetLoginAsync(
		string userName,
		string ipAddress,
		CancellationToken cancellationToken = default);
}


public sealed record RateLimitResult(
	bool Allowed,
	int Attempts,
	int MaxAttempts,
	int RetryAfterSeconds);