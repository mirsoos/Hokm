using Hokm.Application.Events;
using Hokm.Application.Features.AutoPlay.Commands.StartTrumpSelectionTimer;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Contracts;
using Hokm.Application.Realtime.Mappers;
using Hokm.Domain.Entities;
using Hokm.Domain.ValueObjects;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.DealCards.Command
{
    public class DealCardsCommandHandler : IRequestHandler<DealCardsCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;

        public DealCardsCommandHandler(IGameRepository gameRepository, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(DealCardsCommand request, CancellationToken cancellationToken)
        {
            var currentGame = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (currentGame == null)
                throw new ArgumentNullException(nameof(request.GameId), "Game not found.");

            Dictionary<Guid, List<Card>> dealtCards;
            Guid hakemId;

            if (!currentGame.CurrentRoundIndex.HasValue || currentGame.Rounds.Count == 0)
            {
                var randomDealerIndex = Random.Shared.Next(0, currentGame.Players.Count);
                var randomDealerId = currentGame.Players[randomDealerIndex].Id;

                var randomHakemIndex = Random.Shared.Next(0, currentGame.Players.Count);
                hakemId = currentGame.Players[randomHakemIndex].Id;

                dealtCards = currentGame.StartRoundAndDeal(randomDealerId, hakemId);
            }
            else
            {
                dealtCards = currentGame.StartNextRound();

                var currentRound = currentGame.Rounds[currentGame.CurrentRoundIndex!.Value];
                hakemId = currentRound.HakemId;
            }

            await _gameRepository.UpdateAsync(currentGame, cancellationToken);

            foreach (var kv in dealtCards)
            {
                var playerId = kv.Key;
                var cards = kv.Value;
                await _mediator.Publish(new PlayerGameEventNotification(
                        request.GameId,
                        playerId,
                        "your_cards_dealt",
                        JsonSerializer.Serialize(new YourCardsDealtEvent
                        {
                            IsInitialDeal = true,
                            HakemPlayerId = hakemId.ToString(),
                            Cards = cards.Select(RealtimeMapper.ToDto).ToList()
                        }
                        )), cancellationToken);
            }

            var startTimerCmd = new StartTrumpSelectionTimerCommand(currentGame.Id, hakemId);
            await _mediator.Send(startTimerCmd, cancellationToken);

            return Unit.Value;
        }
    }
}