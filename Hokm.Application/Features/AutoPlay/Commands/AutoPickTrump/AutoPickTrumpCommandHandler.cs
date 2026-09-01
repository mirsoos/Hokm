using Hokm.Application.Features.PickTrump.Commands;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Bot;
using MediatR;

namespace Hokm.Application.Features.AutoPlay.Commands.AutoPickTrump
{
    public class AutoPickTrumpCommandHandler : IRequestHandler<AutoPickTrumpCommand, Unit>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IMediator _mediator;

        public AutoPickTrumpCommandHandler(
            IGameRepository gameRepository,
            IMediator mediator)
        {
            _gameRepository = gameRepository;
            _mediator = mediator;
        }

        public async Task<Unit> Handle(AutoPickTrumpCommand request, CancellationToken cancellationToken)
        {
            var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
            if (game == null) return Unit.Value;

            var hakem = game.Players.FirstOrDefault(p => p.Id == request.HakemId);
            if (hakem == null) return Unit.Value;

            if (!hakem.IsAutoPlay)
            {
                hakem.EnableAutoPlay();
                await _gameRepository.UpdateAsync(game, cancellationToken);
            }

            if (!game.CurrentRoundIndex.HasValue || game.Rounds.Count <= game.CurrentRoundIndex.Value)
                return Unit.Value;

            var round = game.Rounds[game.CurrentRoundIndex.Value];
            if (!round.PlayerHands.TryGetValue(request.HakemId, out var firstFiveCards) || firstFiveCards.Count == 0)
                return Unit.Value;

            var bestSuit = HokmBot.DecideTrump(firstFiveCards);

            var pickTrumpCmd = new PickTrumpCommand
            {
                GameId = game.Id,
                DealerId = request.HakemId,
                TrumpSuit = bestSuit
            };

            // استفاده از mediator چون این دستور هم‌اکنون درون لاک Coordinator تایمر قرار دارد
            await _mediator.Send(pickTrumpCmd, cancellationToken);

            return Unit.Value;
        }
    }
}