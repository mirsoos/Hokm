using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Hokm.Application.Realtime.Execution
{
    public sealed class GameCommandEnvelope<TResponse> : IGameCommandEnvelope
    {
        public Guid GameId { get; init; }

        public IRequest<TResponse> Command { get; init; } = default!;

        public TaskCompletionSource<TResponse> CompletionSource { get; init; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken CancellationToken { get; init; }

        public async Task ExecuteAsync(IServiceProvider serviceProvider)
        {
            try
            {
                var mediator = serviceProvider.GetRequiredService<IMediator>();

                var result = await mediator.Send(Command, CancellationToken);

                CompletionSource.TrySetResult(result);
            }
            catch (Exception ex)
            {
                CompletionSource.TrySetException(ex);
            }
        }

        public void TrySetException(Exception ex)
        {
            CompletionSource.TrySetException(ex);
        }
    }
}