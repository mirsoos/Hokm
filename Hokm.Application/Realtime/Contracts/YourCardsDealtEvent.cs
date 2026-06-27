
namespace Hokm.Application.Realtime.Contracts
{
    public sealed class YourCardsDealtEvent
    {
        public List<CardDto> Cards { get; set; } = new();
        public bool IsInitialDeal { get; set; }
    }
}
