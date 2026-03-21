using System.Text;
using EchoPhase.Clients;
using EchoPhase.Controllers.Api.v1.Dto.Discord;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("/api/v1/discord/vault")]
    public class DiscordVaultController : ControllerBase
    {
        private readonly IExternalTokenService _tokenService;
        private readonly IUserService _userService;
        private readonly string _providerName = "Discord";

        public DiscordVaultController(
            IExternalTokenService tokenService,
            IUserService userService)
        {
            _tokenService = tokenService;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var keys = _tokenService.GetKeyNames(user.Id);
            return Ok(keys);
        }

        [HttpPost]
        [ValidateAntiForgery]
        public async Task<IActionResult> Set([FromBody] SetVaultDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var entity = new ExternalToken
            {
                UserId = user.Id,
                ProviderName = _providerName,
                TokenName = dto.Name,
                Value = Encoding.UTF8.GetBytes(dto.Value)
            };

            await _tokenService.SetAsync(entity);
            return NoContent();
        }

        [HttpDelete]
        [ValidateAntiForgery]
        public async Task<IActionResult> Delete([FromBody] DeleteVaultDto dto)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var deleted = await _tokenService.DeleteAsync(user.Id, _providerName, dto.Name);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("all")]
        [ValidateAntiForgery]
        public async Task<IActionResult> DeleteAll()
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            await _tokenService.DeleteAllAsync(user.Id);
            return NoContent();
        }
    }
}
