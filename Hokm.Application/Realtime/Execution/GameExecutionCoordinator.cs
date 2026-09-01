using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using Hokm.Application.Interfaces;

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

        public async Task TryCleanupWorkerAsync(Guid gameId)
        {
            if (!_workers.TryGetValue(gameId, out var worker))
                return;

            using var scope = _scopeFactory.CreateScope();
            var gameRepository = scope.ServiceProvider.GetRequiredService<IGameRepository>();

            var shouldKeepAlive = await gameRepository.IsGameActiveWithHumanPlayersAsync(gameId, CancellationToken.None);

            if (!shouldKeepAlive)
            {
                var dict = (ICollection<KeyValuePair<Guid, GameWorker>>)_workers;
                if (dict.Remove(new KeyValuePair<Guid, GameWorker>(gameId, worker)))
                {
                    await worker.StopAsync();
                }
            }
        }
    }
}