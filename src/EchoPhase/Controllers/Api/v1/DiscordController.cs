using EchoPhase.Clients.Discord;
using EchoPhase.Identity;
using Microsoft.AspNetCore.Mvc;
using EchoPhase.Controllers.Api.v1.Dto.Discord;

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var keys = await _vault.GetUserKeyNamesAsync(user.Id.ToString());
            return Ok(keys);
        }

        [HttpGet("{keyName}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Get(string keyName)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _vault.GetAsync<string>(user.Id.ToString(), keyName);
            if (!result.Successful)
                return NotFound();

            return Ok(result.Value);
        }

        [HttpPost("{keyName}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Set(string keyName, [FromBody] SetVaultDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _vault.SetAsync(user.Id.ToString(), keyName, dto.Value);
            if (!result)
                return StatusCode(StatusCodes.Status500InternalServerError);

            return NoContent();
        }

        [HttpDelete("{keyName}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string keyName)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _vault.DeleteAsync(user.Id.ToString(), keyName);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete]
        [ValidateAntiForgeryToken]
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
