using System.Threading.Channels;

namespace Hokm.Presentation.gRPC.Realtime
{
    public sealed class GameSubscription
    {
        public Guid SubscriptionId { get; }
        public Guid GameId { get; }
        public Guid PlayerId { get; }
        public Channel<GameEvent> EventChannel { get; }
        public DateTime ConnectedAtUtc { get; }

        public GameSubscription(Guid gameId, Guid playerId)
        {
            SubscriptionId = Guid.NewGuid();
            GameId = gameId;
            PlayerId = playerId;
            ConnectedAtUtc = DateTime.UtcNow;

            EventChannel = Channel.CreateBounded<GameEvent>(
                new BoundedChannelOptions(100)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropOldest
                });
        }
    }
}