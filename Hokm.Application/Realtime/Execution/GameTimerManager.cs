using Hokm.Application.Features.AutoPlay.Commands;
using Hokm.Application.Features.AutoPlay.Commands.AutoPickTrump;
using Hokm.Application.Features.AutoPlay.Commands.AutoPlay;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Hokm.Application.Realtime.Execution
{
    public sealed class GameTimerManager
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeTimers = new();
        private readonly IServiceScopeFactory _scopeFactory;

        public GameTimerManager(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public void StartTimer(Guid gameId, Guid playerId, double seconds, bool isTrumpSelection = false)
        {
            CancelTimer(gameId);

            var cts = new CancellationTokenSource();
            _activeTimers[gameId] = cts;

            BroadcastTimerStartedEvent(gameId, playerId, seconds);

            _ = RunTimeoutTaskAsync(gameId, playerId, seconds, isTrumpSelection, cts.Token);
        }

        public void CancelTimer(Guid gameId)
        {
            if (_activeTimers.TryRemove(gameId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private async Task RunTimeoutTaskAsync(Guid gameId, Guid playerId, double seconds, bool isTrumpSelection, CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);

                if (!token.IsCancellationRequested)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var coordinator = scope.ServiceProvider.GetRequiredService<GameExecutionCoordinator>();

                    if (isTrumpSelection)
                    {
                        var autoPickTrumpCmd = new AutoPickTrumpCommand(gameId, playerId);
                        await coordinator.ExecuteAsync(gameId, autoPickTrumpCmd, CancellationToken.None);
                    }
                    else
                    {
                        var autoPlayCmd = new AutoPlayCardCommand(gameId, playerId);
                        await coordinator.ExecuteAsync(gameId, autoPlayCmd, CancellationToken.None);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // تایمر با موفقیت لغو شد (بازیکن واقعی به موقع بازی کرد)
            }
        }

        private void BroadcastTimerStartedEvent(Guid gameId, Guid playerId, double seconds)
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        }
    }
}
