namespace Hokm.Application.Realtime.Execution
{
    public interface IGameCommandEnvelope
    {
        Guid GameId { get; }
        Task ExecuteAsync(IServiceProvider serviceProvider);
        void TrySetException(Exception ex);
    }
}