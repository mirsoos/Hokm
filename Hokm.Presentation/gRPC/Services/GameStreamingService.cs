using Hokm.Application.Events;
using Hokm.Presentation.gRPC.Realtime;
using MediatR;
using System.Collections.Concurrent;

namespace Hokm.Presentation.gRPC.Services
{
    public sealed class GameStreamingService : INotificationHandler<GameEventNotification> , INotificationHandler<PlayerGameEventNotification>
    {
        private readonly ConcurrentDictionary<Guid,List<GameSubscription>> _subscriptions = new();

        public GameSubscription Subscribe(Guid gameId,Guid playerId)
        {
            var subscription = new GameSubscription(gameId, playerId);

            _subscriptions.AddOrUpdate(
                gameId,
                _ => new List<GameSubscription>
                {
                    subscription
                },
                (_, existing) =>
                {
                    lock (existing)
                    {
                        existing.Add(subscription);
                        return existing;
                    }
                });

            return subscription;
        }

        public void Unsubscribe(GameSubscription subscription)
        {
            subscription.EventChannel.Writer.Complete();

            if (_subscriptions.TryGetValue(subscription.GameId,out var existing))
            {
                lock (existing)
                {
                    existing.RemoveAll(x => x.SubscriptionId == subscription.SubscriptionId);
                    if (existing.Count == 0)
                    {
                        _subscriptions.TryRemove(
                            subscription.GameId,
                            out _);
                    }
                }
            }
        }

        public async Task BroadcastAsync(Guid gameId,GameEvent gameEvent,CancellationToken cancellationToken)
        {
            if (!_subscriptions.TryGetValue(gameId,out var subscribers))
            {
                return;
            }

            List<GameSubscription> snapshot;

            lock (subscribers)
            {
                snapshot = subscribers.ToList();
            }

            foreach (var subscription in snapshot)
            {
                try
                {
                    await subscription.EventChannel.Writer.WriteAsync(gameEvent,cancellationToken);
                }
                catch
                {
                }
            }
        }

        public async Task SendToPlayerAsync(Guid gameId,Guid playerId,GameEvent gameEvent,CancellationToken cancellationToken)
        {
            if (!_subscriptions.TryGetValue(gameId,out var subscribers))
            {
                return;
            }

            GameSubscription? target;

            lock (subscribers)
            {
                target = subscribers.FirstOrDefault(x => x.PlayerId == playerId);
            }

            if (target == null)
            {
                return;
            }

            try
            {
                await target.EventChannel.Writer.WriteAsync(gameEvent,cancellationToken);
            }
            catch
            {
            }
        }

        public async Task Handle(GameEventNotification notification,CancellationToken cancellationToken)
        {
            var gameEvent = new GameEvent
            {
                EventType = notification.EventType,
                Payload = notification.Payload
            };

            await BroadcastAsync(notification.GameId,gameEvent,cancellationToken);
        }

        public async Task Handle(PlayerGameEventNotification notification,CancellationToken cancellationToken)
        {
            var gameEvent = new GameEvent
            {
                EventType = notification.EventType,
                Payload = notification.Payload
            };
            await SendToPlayerAsync(notification.GameId,notification.PlayerId,gameEvent,cancellationToken);
        }

        public int GetConnectedCount(Guid gameId)
        {
            if (_subscriptions.TryGetValue(gameId, out var list))
            {
                lock (list)
                {
                    return list.Count;
                }
            }
            return 0;
        }
    }
}