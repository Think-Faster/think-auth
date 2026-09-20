using System.ComponentModel.DataAnnotations;

namespace WebAPI.Contracts
{
	public class LoginRequest
	{
		[Required]
		[MinLength(3)]
		public string UserName { get; set; } = string.Empty;

		[Required]
		public string Password { get; set; } = string.Empty;
	}
}
