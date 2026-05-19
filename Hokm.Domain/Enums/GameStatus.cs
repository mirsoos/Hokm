
namespace Hokm.Domain.Enums
{
    public enum GameStatus
    {
        WaitingForPlayers,

        WaitingForTeams,

        TeamsReady,

        RoundStarting,

        DealingFirstFiveCards,

        WaitingForTrumpSelection,

        DealingRemainingCards,

        Playing,

        RoundFinished,

        Finished
    }
}