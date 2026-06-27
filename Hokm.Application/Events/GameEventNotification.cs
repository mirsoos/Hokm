using MediatR;

namespace Hokm.Application.Events
{
    public class GameEventNotification : INotification
    {
        public Guid GameId { get; }
        public string EventType { get; }
        public string Payload { get; }

        public GameEventNotification(Guid gameId, string eventType, string payload)
        {
            GameId = gameId;
            EventType = eventType;
            Payload = payload;
        }
    }
}
