using Newtonsoft.Json;

namespace AuthService.Models
{
	/// <summary>
	/// Базовая модель для всех сущностей.
	/// </summary>
	public class EntityBase
	{
		[JsonProperty("id")]
		public Guid Id { get; set; } = Guid.NewGuid();

		[JsonProperty("createdBy")]
		public Guid CreatedBy { get; set; }

		[JsonProperty("updatedBy")]
		public Guid UpdatedBy { get; set; }

		[JsonProperty("createdAt")]
		public DateTimeOffset CreatedAt { get; set; } = DateTime.UtcNow;

		[JsonProperty("updatedAt")]
		public DateTimeOffset UpdatedAt { get; set; } = DateTime.UtcNow;
	}
}
