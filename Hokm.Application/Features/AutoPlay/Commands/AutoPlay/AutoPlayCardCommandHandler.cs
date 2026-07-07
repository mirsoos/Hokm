using Hokm.Application.Events;
using Hokm.Application.Features.PlayCard.Commands;
using Hokm.Application.Features.Snapshot.Queries;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Bot;
using MediatR;
using System.Text.Json;

namespace Hokm.Application.Features.AutoPlay.Commands.AutoPlay
{

    public class AutoPlayCardCommandHandler : IRequestHandler<AutoPlayCardCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;

        public AutoPlayCardCommandHandler(IGameRepository gameRepository, IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(AutoPlayCardCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            if (game.GetCurrentTurnPlayerId() != request.PlayerId)
                return Unit.Value;

            var player = game.Players.First(p => p.Id == request.PlayerId);
            player.EnableAutoPlay();
            await _gameRepository.UpdateAsync(game, cancellationToken);

            var chosenCard = HokmBot.DecideCardToPlay(game, request.PlayerId);
            if (chosenCard == null) return Unit.Value;

            var playCardCmd = new PlayCardCommand
            {
                GameId = game.Id,
                PlayerId = request.PlayerId,
                Suit = chosenCard.Suit,
                Rank = chosenCard.Rank
            };

            await _mediator.Send(playCardCmd, cancellationToken);

            await _mediator.Publish(new GameEventNotification(
                game.Id,
                "player_status_changed",
                JsonSerializer.Serialize(new { PlayerId = request.PlayerId.ToString(), IsOnline = true, IsAutoPlay = true })
            ), cancellationToken);


            var snapshotQuery = new GetGameSnapshotQuery { GameId = game.Id, PlayerId = request.PlayerId };
            var snapshot = await _mediator.Send(snapshotQuery, cancellationToken);

            await _mediator.Publish(new PlayerGameEventNotification(
                game.Id,
                request.PlayerId,
                "game_state_updated",
                JsonSerializer.Serialize(new { GameState = snapshot })
            ), cancellationToken);

            return Unit.Value;
        }
    }
}
