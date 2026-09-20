using AuthService.Cryptography.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
	public class PublicKeyController : ControllerBase
	{
		private readonly ITokenService _tokenService;

		public PublicKeyController(
			ITokenService tokenService)
		{
			_tokenService = tokenService;
		}

		[AllowAnonymous]
		[HttpGet(".well-known/jwks")]
		public IActionResult GetPublicKey()
		{
			var publicKey = _tokenService.GetPublicKey();

			return Content(
				publicKey,
				"application/x-pem-file");
		}
	}
}
