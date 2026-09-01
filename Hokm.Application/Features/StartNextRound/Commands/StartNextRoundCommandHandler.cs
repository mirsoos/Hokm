using Hokm.Application.Constants;
using Hokm.Application.DTOs.GameSnapshot;
using Hokm.Application.Events;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.StartNextRound.Commands
{
    public class StartNextRoundCommandHandler : IRequestHandler<StartNextRoundCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;
        private readonly GameTimerManager _timerManager;

        public StartNextRoundCommandHandler(
            IGameRepository gameRepository,
            IMediator mediator,
            GameTimerManager timerManager)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
            _timerManager = timerManager;
        }

        public async Task<Unit> Handle(StartNextRoundCommand request, CancellationToken cancellationToken)
        {
            var bgGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (bgGame == null)
                return Unit.Value;

            if (bgGame.Status != Domain.Enums.GameStatus.RoundFinished)
                return Unit.Value;

            var newDealtCards = bgGame.StartNextRound();
            await _gameRepository.UpdateAsync(bgGame, cancellationToken);

            var newActiveRound = bgGame.Rounds[bgGame.CurrentRoundIndex!.Value];
            var newHakemPlayer = bgGame.Players.First(x => x.Id == newActiveRound.HakemId);

            if (newHakemPlayer.IsAutoPlay)
            {
                await _timerManager.StartTimer(bgGame.Id, newHakemPlayer.Id, 1.5, isTrumpSelection: true);
            }
            else
            {
                await _timerManager.StartTimer(bgGame.Id, newHakemPlayer.Id, GameConstants.HumanTurnTimeoutSeconds, isTrumpSelection: true);
            }

            foreach (var player in bgGame.Players)
            {
                if (newDealtCards.TryGetValue(player.Id, out var newHand))
                {
                    var handDto = newHand.Select(c => new CardDto
                    {
                        Suit = c.Suit.ToString(),
                        Rank = c.Rank.ToString(),
                        IsPlayable = true
                    }).ToList();

                    await _mediator.Publish(new PlayerGameEventNotification(
                        bgGame.Id,
                        player.Id,
                        "your_cards_dealt",
                        JsonSerializer.Serialize(new
                        {
                            IsInitialDeal = true,
                            Cards = handDto,
                            HakemPlayerId = newHakemPlayer.Id.ToString()
                        })
                    ), cancellationToken);
                }
            }

            return Unit.Value;
        }
    }
}