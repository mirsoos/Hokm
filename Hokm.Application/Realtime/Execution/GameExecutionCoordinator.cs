using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Hokm.Application.Realtime.Execution
{
    public sealed class GameExecutionCoordinator
    {
        private readonly ConcurrentDictionary<Guid, GameWorker> _workers;

        private readonly IServiceScopeFactory _scopeFactory;

        public GameExecutionCoordinator(
            IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;

            _workers = new ConcurrentDictionary<Guid, GameWorker>();
        }

        public async Task<TResponse> ExecuteAsync<TResponse>(
            Guid gameId,
            IRequest<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            var worker = GetOrCreateWorker(gameId);

            var envelope = new GameCommandEnvelope<TResponse>
            {
                GameId = gameId,
                Command = command,
                CancellationToken = cancellationToken
            };

            return await worker.EnqueueAsync(envelope);
        }

        private GameWorker GetOrCreateWorker(Guid gameId)
        {
            return _workers.GetOrAdd(
                gameId,
                id => new GameWorker(id, _scopeFactory));
        }
    }
}