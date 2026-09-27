using System.Text.Json;
using StackExchange.Redis;

namespace WebAPI.Services.Audit
{
	/// <summary>
	/// Журнал действий (docs/common/права-и-аудит.md §6.2): событие уходит в поток Redis `audit`
	/// (XADD audit * event &lt;json&gt;, §6.5), в базу его пишет сервис аудита. Тот же Redis, что у
	/// ограничения попыток входа. Аудит не роняет запрос: Redis недоступен — событие в лог.
	/// Пароли и токены сюда не передаются никогда (§6.3).
	/// </summary>
	public sealed class AuditWriter
	{
		private const string Stream = "audit";
		private const int MaxLength = 1_000_000;

		private static readonly JsonSerializerOptions Json = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
		};

		private readonly IConnectionMultiplexer _redis;
		private readonly IHttpContextAccessor _http;
		private readonly ILogger<AuditWriter> _logger;

		public AuditWriter(IConnectionMultiplexer redis, IHttpContextAccessor http, ILogger<AuditWriter> logger)
		{
			_redis = redis;
			_http = http;
			_logger = logger;
		}

		public async Task WriteAsync(
			string eventType,
			string outcome,
			Guid? actorId,
			string? actorLogin,
			Dictionary<string, object?>? details = null)
		{
			var context = _http.HttpContext;
			var payload = JsonSerializer.Serialize(new
			{
				EventId = Guid.NewGuid().ToString(),
				OccurredAt = DateTimeOffset.UtcNow,
				Service = "auth",
				EventType = eventType,
				Outcome = outcome,
				ActorKind = actorId is null && actorLogin is null ? "anonymous" : "user",
				ActorId = actorId?.ToString(),
				ActorLogin = actorLogin,
				RequestId = context?.Request.Headers["X-Request-ID"].FirstOrDefault() ?? context?.TraceIdentifier,
				Ip = ClientIp(context),
				ObjectType = "user",
				ObjectId = actorId?.ToString(),
				AreaId = (int?)null,
				Details = details ?? new Dictionary<string, object?>()
			}, Json);

			try
			{
				await _redis.GetDatabase().StreamAddAsync(
					Stream, "event", payload, maxLength: MaxLength, useApproximateMaxLength: true);
			}
			catch (Exception ex)
			{
				_logger.LogWarning("Audit stream unavailable ({Error}): {AuditEvent}", ex.GetType().Name, payload);
			}
		}

		private static string? ClientIp(HttpContext? context)
		{
			if (context is null)
			{
				return null;
			}

			// За nginx адрес клиента — первый в X-Forwarded-For (или X-Real-IP).
			var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
			return !string.IsNullOrEmpty(forwarded)
				? forwarded
				: context.Request.Headers["X-Real-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString();
		}
	}
}
