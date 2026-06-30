using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace Hokm.Application.Realtime.Execution
{
    public sealed class GameWorker
    {
        public Guid GameId { get; }

        private readonly Channel<IGameCommandEnvelope> _queue;

        private readonly IServiceScopeFactory _scopeFactory;

        private readonly CancellationTokenSource _cts;

        private readonly Task _processingTask;

        public DateTime LastActivityUtc { get; private set; }

        public GameWorker(Guid gameId , IServiceScopeFactory scopeFactory)
        {
            GameId = gameId;

            _scopeFactory = scopeFactory;

            _cts = new CancellationTokenSource();

            _queue = Channel.CreateUnbounded<IGameCommandEnvelope>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

            LastActivityUtc = DateTime.UtcNow;

            _processingTask = Task.Run(ProcessLoopAsync);
        }

        public async Task<TResponse> EnqueueAsync<TResponse>(GameCommandEnvelope<TResponse> envelope)
        {
            await _queue.Writer.WriteAsync(envelope);

            return await envelope.CompletionSource.Task;
        }

        private async Task ProcessLoopAsync()
        {
            await foreach (var envelope in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    LastActivityUtc = DateTime.UtcNow;

                    using var scope = _scopeFactory.CreateScope();

                    await envelope.ExecuteAsync(scope.ServiceProvider);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"❌ خطا در پردازش تسک بازی: {ex}");
                }
            }
        }

        public async Task StopAsync()
        {
            _queue.Writer.TryComplete();

            _cts.Cancel();

            try
            {
                await _processingTask;
            }
            catch
            {

            }
            _cts.Dispose();
        }
    }
}