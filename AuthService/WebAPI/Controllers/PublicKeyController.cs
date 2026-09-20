using AuthService.Cryptography.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
	[ApiController]
	[Route("api/auth/.well-known")]
	public class PublicKeyController : ControllerBase
	{
		private readonly ITokenService _tokenService;

		public PublicKeyController(
			ITokenService tokenService)
		{
			_tokenService = tokenService;
		}

		[AllowAnonymous]
		[HttpGet("public-key")]
		[Produces("application/x-pem-file")]
		public IActionResult GetPublicKey()
		{
			var publicKey = _tokenService.GetPublicKey();

			return Content(
				publicKey,
				"application/x-pem-file");
		}
	}
}
