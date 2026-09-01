using Hokm.Application.Events;
using Hokm.Presentation.gRPC.Realtime;
using MediatR;
using System.Collections.Concurrent;

namespace Hokm.Presentation.gRPC.Services
{
    public sealed class GameStreamingService : INotificationHandler<GameEventNotification>, INotificationHandler<PlayerGameEventNotification>
    {
        private readonly ConcurrentDictionary<Guid, List<GameSubscription>> _subscriptions = new();

        public GameSubscription Subscribe(Guid gameId, Guid playerId)
        {
            var subscription = new GameSubscription(gameId, playerId);

            _subscriptions.AddOrUpdate(
                gameId,
                _ => new List<GameSubscription> { subscription },
                (_, existing) =>
                {
                    lock (existing)
                    {
                        var oldSubs = existing.Where(x => x.PlayerId == playerId).ToList();
                        foreach (var old in oldSubs)
                        {
                            try { old.EventChannel.Writer.TryComplete(); } catch { }
                        }
                        existing.RemoveAll(x => x.PlayerId == playerId);

                        existing.Add(subscription);
                        return existing;
                    }
                });

            return subscription;
        }

        public void Unsubscribe(GameSubscription subscription)
        {
            try
            {
                subscription.EventChannel.Writer.TryComplete();
            }
            catch { }

            if (_subscriptions.TryGetValue(subscription.GameId, out var existing))
            {
                lock (existing)
                {
                    existing.RemoveAll(x => x.SubscriptionId == subscription.SubscriptionId);

                    if (existing.Count == 0)
                    {
                        var dict = (ICollection<KeyValuePair<Guid, List<GameSubscription>>>)_subscriptions;
                        dict.Remove(new KeyValuePair<Guid, List<GameSubscription>>(subscription.GameId, existing));
                    }
                }
            }
        }

        public bool IsPlayerSubscribed(Guid gameId, Guid playerId)
        {
            if (_subscriptions.TryGetValue(gameId, out var list))
            {
                lock (list)
                {
                    return list.Any(x => x.PlayerId == playerId);
                }
            }
            return false;
        }

        public async Task BroadcastAsync(Guid gameId, GameEvent gameEvent, CancellationToken cancellationToken)
        {
            if (!_subscriptions.TryGetValue(gameId, out var subscribers)) return;

            List<GameSubscription> snapshot;
            lock (subscribers)
            {
                snapshot = subscribers.ToList();
            }

            foreach (var subscription in snapshot)
            {
                try
                {
                    await subscription.EventChannel.Writer.WriteAsync(gameEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ خطا در ارسال رویداد به بازیکن {subscription.PlayerId} در بازی {gameId}. حذف Subscription. خطا: {ex.Message}");
                    Unsubscribe(subscription);
                }
            }
        }

        public async Task SendToPlayerAsync(Guid gameId, Guid playerId, GameEvent gameEvent, CancellationToken cancellationToken)
        {
            if (!_subscriptions.TryGetValue(gameId, out var subscribers)) return;

            GameSubscription? target;
            lock (subscribers)
            {
                target = subscribers.LastOrDefault(x => x.PlayerId == playerId);
            }

            if (target == null) return;

            try
            {
                await target.EventChannel.Writer.WriteAsync(gameEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ خطا در ارسال رویداد تکی به بازیکن {playerId} در بازی {gameId}. حذف Subscription. خطا: {ex.Message}");
                Unsubscribe(target);
            }
        }

        public async Task Handle(GameEventNotification notification, CancellationToken cancellationToken)
        {
            var gameEvent = new GameEvent
            {
                EventType = notification.EventType,
                Payload = notification.Payload
            };
            await BroadcastAsync(notification.GameId, gameEvent, cancellationToken);
        }

        public async Task Handle(PlayerGameEventNotification notification, CancellationToken cancellationToken)
        {
            var gameEvent = new GameEvent
            {
                EventType = notification.EventType,
                Payload = notification.Payload
            };
            await SendToPlayerAsync(notification.GameId, notification.PlayerId, gameEvent, cancellationToken);
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