using Hokm.Application.Events;
using MediatR;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Hokm.GrpcService.Services
{
    public class GameStreamingService : INotificationHandler<GameEventNotification>
    {
        private readonly ConcurrentDictionary<Guid, Channel<GameEvent>> _gameChannels = new();
        public Channel<GameEvent> Subscribe(Guid gameId)
        {
            var channel = Channel.CreateUnbounded<GameEvent>();
            _gameChannels[gameId] = channel;
            return channel;
        }
        public void UnSubscribe(Guid gameId, Channel<GameEvent> channel)
        {
            channel.Writer.Complete();
            if (_gameChannels.TryGetValue(gameId, out var existing) && existing == channel)
            {
                _gameChannels.TryRemove(gameId, out _);
            }
        }
        public async Task Handle(GameEventNotification notification, CancellationToken cancellationToken)
        {
            if(_gameChannels.TryGetValue(notification.GameId,out var channel))
            {
                var gameEvent = new GameEvent
                {
                    EventType = notification.EventType,
                    Payload = notification.Payload
                };
                try
                {
                    await channel.Writer.WriteAsync(gameEvent, cancellationToken);
                }
                catch{ }
            }
        }
    }
}
