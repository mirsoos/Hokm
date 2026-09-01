using Hokm.Application.Constants;
using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.AutoPlay.Commands.ResumeControl
{
    public class ResumeControlCommandHandler : IRequestHandler<ResumeControlCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly GameTimerManager _timerManager;
        private readonly IMediator _mediator;

        public ResumeControlCommandHandler(IGameRepository gameRepository, GameTimerManager timerManager, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _timerManager = timerManager;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(ResumeControlCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId);
            if (player != null)
            {
                player.DisableAutoPlay();
                await _gameRepository.UpdateAsync(game, cancellationToken);

                // اعلام به همه که بازیکن مجدداً کنترل را به دست گرفت
                await _mediator.Publish(new GameEventNotification(
                    game.Id,
                    "player_status_changed",
                    JsonSerializer.Serialize(new
                    {
                        PlayerId = request.PlayerId.ToString(),
                        IsOnline = true,
                        IsAutoPlay = false
                    })
                ), cancellationToken);

                // بررسی فاز تعیین حکم
                bool isTrumpPhase = game.Status == GameStatus.WaitingForTrumpSelection ||
                                    (game.CurrentRoundIndex.HasValue && !game.Rounds[game.CurrentRoundIndex.Value].TrumpSuit.HasValue);

                if (isTrumpPhase && game.CurrentRoundIndex.HasValue)
                {
                    var activeRound = game.Rounds[game.CurrentRoundIndex.Value];
                    var dealer = game.Players.First(x => x.Id == activeRound.DealerId);
                    var hakemSide = game.GetRightSideOf(dealer.PlayerSide);
                    var hakem = game.Players.First(x => x.PlayerSide == hakemSide);

                    // 👈 فقط اگر نوبت خود این بازیکن است، تایمرش بازتنظیم شود
                    if (hakem.Id == request.PlayerId)
                    {
                        _timerManager.CancelTimer(game.Id);
                        await _timerManager.StartTimer(game.Id, request.PlayerId, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: true);
                    }
                }
                // بررسی فاز بازی ورق
                else if (game.Status == GameStatus.Playing && game.GetCurrentTurnPlayerId() == request.PlayerId)
                {
                    // 👈 فقط اگر نوبت خود این بازیکن است، تایمرش بازتنظیم شود
                    _timerManager.CancelTimer(game.Id);
                    await _timerManager.StartTimer(game.Id, request.PlayerId, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: false);
                }
            }
            return Unit.Value;
        }
    }
}