using Hokm.Application.Features.Game.Queries.GetGameHistory;
using Hokm.Application.Features.profile.Commands.UpdateProfile;
using Hokm.Application.Features.profile.Queries.GetProfile;
using Hokm.Application.Features.profile.Queries.GetStats;
using Hokm.Application.Features.Profile.Commands.DeleteUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Hokm.Presentation.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetProfileQuery(userId), cancellationToken);

            return result.Match<IActionResult>(
                success => Ok(success),
                errors => BadRequest(new
                {
                    Success = false,
                    Message = errors.First().Description
                })
            );
        }

        // PUT /api/user/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileCommand request,CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            request.UserId = userId;

            var result = await _mediator.Send(request, cancellationToken);

            return result.Match<IActionResult>(
                success => Ok(success),
                errors => BadRequest(new
                {
                    Success = false,
                    Message = errors.First().Description
                })
            );
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetStatsQuery(userId), cancellationToken);

            return result.Match<IActionResult>(
                success => Ok(success),
                errors => BadRequest(new
                {
                    Success = false,
                    Message = errors.First().Description
                })
            );
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetGameHistory(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new GetGameHistoryQuery(userId,10), cancellationToken);

            return result.Match<IActionResult>(
                success => Ok(success),
                errors => BadRequest(new
                {
                    Success = false,
                    Message = errors.First().Description
                })
            );
        }

        [HttpDelete("DeleteUser")]
        public async Task<IActionResult> DeleteUser(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var result = await _mediator.Send(new DeleteUserCommand(userId), cancellationToken);

            return result.Match<IActionResult>(
                success => Ok(success),
                errors => BadRequest(new
                {
                    Success = false,
                    Message = errors.First().Description
                })
            );
        }

        [HttpPost("leave-active-game")]
        public async Task<IActionResult> LeaveActiveGame(CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            if (gRPC.Services.HokmGameService.PlayerActiveGames.TryGetValue(userId, out var gameId))
            {
                var statusPayload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    PlayerId = userId.ToString(),
                    IsOnline = false
                });

                await _mediator.Publish(new Application.Events.GameEventNotification(
                    gameId,
                    "player_status_changed",
                    statusPayload
                ), cancellationToken);

                gRPC.Services.HokmGameService.PlayerActiveGames.TryRemove(userId, out _);
            }

            return Ok(new { Success = true });
        }

        [HttpGet("active-game")]
        public async Task<IActionResult> GetActiveGame(CancellationToken cancellationToken)
        {
            var userId = GetUserId();

            if (gRPC.Services.HokmGameService.PlayerActiveGames.TryGetValue(userId, out var gameId))
            {
                try
                {
                    var query = new Application.Features.GameStarted.Queries.GetGameStateQuery
                    {
                        GameId = gameId
                    };
                    var gameResult = await _mediator.Send(query, cancellationToken);

                    if (gameResult == null || gameResult.Status.ToString().Equals("Finished", StringComparison.OrdinalIgnoreCase))
                    {
                        gRPC.Services.HokmGameService.PlayerActiveGames.TryRemove(userId, out _);
                        return NotFound(new { Message = "هیچ بازی فعالی برای این کاربر یافت نشد." });
                    }

                    return Ok(new { GameId = gameId.ToString() });
                }
                catch (Exception)
                {
                    gRPC.Services.HokmGameService.PlayerActiveGames.TryRemove(userId, out _);
                }
            }

            return NotFound(new { Message = "هیچ بازی فعالی برای این کاربر یافت نشد." });
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("User ID not found in token");

            return userId;
        }
    }
}
