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
            TableKind.Speedy => 200,
            TableKind.Pro => 1500,
            TableKind.Vip => 10000,
            _ => 0
        };

        public static int GetTablePrize(TableKind tableKind) => tableKind switch
        {
            TableKind.Bot => 0,
            TableKind.Speedy => 500,
            TableKind.Pro => 3500,
            TableKind.Vip => 30000,
            _ => 0
        };

        public static int GetTableWinXp(TableKind tableKind) => tableKind switch
        {
            TableKind.Bot => 20,
            TableKind.Speedy => 100,
            TableKind.Pro => 250,
            TableKind.Vip => 500,
            _ => 100
        };

        public static int GetTableLossXp(TableKind tableKind) => tableKind switch
        {
            TableKind.Bot => 5,
            TableKind.Speedy => 25,
            TableKind.Pro => 50,
            TableKind.Vip => 100,
            _ => 25
        };
    }
}