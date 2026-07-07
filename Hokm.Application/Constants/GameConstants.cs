using Hokm.Domain.Enums;

namespace Hokm.Application.Constants
{
    public static class GameConstants
    {
        public const double HumanTurnTimeoutSeconds = 20.0;
        public const double BotTurnTimeoutSeconds = 1.5;
        public const double DealingAnimationDelaySeconds = 7.0;

        public static int GetTableFee(TableKind tableKind) => tableKind switch
        {
            TableKind.Bot => 0,
            TableKind.Speedy => 100,
            TableKind.Pro => 500,
            TableKind.Vip => 1000,
            _ => 0
        };
    }
}
