using MediatR;

namespace Hokm.Application.Events
{
    public class PlayerGameEventNotification : INotification
    {
        public Guid GameId { get; }

        public Guid PlayerId { get; }

        public string EventType { get; }

        public string Payload { get; }

        public PlayerGameEventNotification(Guid gameId,Guid playerId,string eventType,string payload)
        {
            GameId = gameId;
            PlayerId = playerId;
            EventType = eventType;
            Payload = payload;
        }
    }
}
