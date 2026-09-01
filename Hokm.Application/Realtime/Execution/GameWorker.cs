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

        public GameWorker(Guid gameId, IServiceScopeFactory scopeFactory)
        {
            GameId = gameId;

            _scopeFactory = scopeFactory;

            _cts = new CancellationTokenSource();

            _queue = Channel.CreateBounded<IGameCommandEnvelope>(
                new BoundedChannelOptions(50)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropWrite
                });

            LastActivityUtc = DateTime.UtcNow;

            _processingTask = ProcessLoopAsync();
        }

        public async Task<TResponse> EnqueueAsync<TResponse>(GameCommandEnvelope<TResponse> envelope)
        {
            var success = _queue.Writer.TryWrite(envelope);

            if (!success)
            {
                envelope.CompletionSource.TrySetException(
                    new InvalidOperationException("صف دستورات بازی پر است. لطفاً دوباره تلاش کنید."));
            }

            return await envelope.CompletionSource.Task;
        }

        private async Task ProcessLoopAsync()
        {
            try
            {
                await foreach (var envelope in _queue.Reader.ReadAllAsync(_cts.Token))
                {
                    try
                    {
                        LastActivityUtc = DateTime.UtcNow;

                        using var scope = _scopeFactory.CreateScope();

                        await envelope.ExecuteAsync(scope.ServiceProvider);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ خطا در پردازش تسک بازی: {ex}");

                        try
                        {
                            envelope.TrySetException(ex);
                        }
                        catch (Exception setEx)
                        {
                            Console.WriteLine($"❌ خطای بحرانی در SetException: {setEx}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // خروج طبیعی حلقه در هنگام متوقف شدن ورکر
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطای غیرمنتظره در حلقه اجرای GameWorker {GameId}: {ex}");
            }
        }

        public async Task StopAsync()
        {
            _queue.Writer.TryComplete();

            try
            {
                _cts.Cancel();
            }
            catch { }

            try
            {
                await _processingTask;
            }
            catch
            {
            }
            finally
            {
                _cts.Dispose();
            }
        }
    }
}