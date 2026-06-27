
namespace Hokm.Application.Realtime.Contracts
{
    public sealed class PlayerPlayedCardEvent
    {
        public Guid PlayerId { get; set; }
        public Guid? NextPlayerId { get; set; }
        public CardDto Card { get; set; } = default!;
    }
}
