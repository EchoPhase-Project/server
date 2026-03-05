using EchoPhase.Clients.Discord;
using EchoPhase.Controllers.Api.v1.Dto.Discord;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("/api/v1/discord/vault")]
    public class DiscordVaultController : ControllerBase
    {
        private readonly IDiscordSecretVault _vault;
        private readonly IUserService _userService;

        public DiscordVaultController(IDiscordSecretVault vault, IUserService userService)
        {
            _vault = vault;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var keys = await _vault.GetUserKeyNamesAsync(user.Id.ToString());
            return Ok(keys);
        }

        [HttpPost]
        [ValidateAntiForgery]
        public async Task<IActionResult> Set([FromBody] SetVaultDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _vault.SetAsync(user.Id.ToString(), dto.Name, dto.Value);
            if (!result)
                return StatusCode(StatusCodes.Status500InternalServerError);

            return NoContent();
        }

        [HttpDelete]
        [ValidateAntiForgery]
        public async Task<IActionResult> Delete([FromBody] DeleteVaultDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _vault.DeleteAsync(user.Id.ToString(), dto.Name);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete]
        [ValidateAntiForgery]
        public async Task<IActionResult> DeleteAll()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            await _vault.DeleteAllAsync(user.Id.ToString());
            return NoContent();
        }
    }
}

