using Hokm.Application.Constants;
using Hokm.Application.Events;
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

        public async Task StartTimer(Guid gameId, Guid playerId, double seconds, bool isTrumpSelection = false)
        {
            CancelTimer(gameId);

            var cts = new CancellationTokenSource();
            _activeTimers[gameId] = cts;

            // ارسال تایمر واقعی به فرانت‌اند
            await BroadcastTimerStartedEventAsync(gameId, playerId, seconds);

            _ = RunTimeoutTaskAsync(gameId, playerId, seconds, isTrumpSelection, cts.Token);
        }

        public void StartFailSafeTimer(Guid gameId, Guid playerId, double seconds, bool isTrumpSelection = false)
        {
            CancelTimer(gameId);

            var cts = new CancellationTokenSource();
            _activeTimers[gameId] = cts;

            _ = RunFailSafeTimeoutTaskAsync(gameId, playerId, seconds, isTrumpSelection, cts.Token);
        }

        public void CancelTimer(Guid gameId)
        {
            if (_activeTimers.TryRemove(gameId, out var cts))
            {
                try
                {
                    if (!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        public void CancelTimer(Guid gameId, Guid playerId)
        {
            CancelTimer(gameId);
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
            catch (OperationCanceledException)
            {
                // لغو تایمر با موفقیت
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RunTimeoutTaskAsync: {ex.Message}");
            }
        }

        private async Task RunFailSafeTimeoutTaskAsync(Guid gameId, Guid playerId, double seconds, bool isTrumpSelection, CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);

                if (!token.IsCancellationRequested)
                {
                    double timeoutSeconds = GameConstants.HumanTurnTimeoutSeconds;
                    await StartTimer(gameId, playerId, timeoutSeconds, isTrumpSelection);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RunFailSafeTimeoutTaskAsync: {ex.Message}");
            }
        }

        private async Task BroadcastTimerStartedEventAsync(Guid gameId, Guid playerId, double seconds)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                // 👈 ⚡ حل قطعی باگ: ارسال زمان واقعی (مثلاً ۱ ثانیه برای ربات و ۲۰ ثانیه برای انسان)
                double clientSeconds = seconds;

                var timerEvent = new GameEventNotification(
                    gameId,
                    "turn_timer_started",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        PlayerId = playerId.ToString(),
                        Seconds = clientSeconds
                    })
                );

                await mediator.Publish(timerEvent, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting timer event: {ex.Message}");
            }
        }
    }
}