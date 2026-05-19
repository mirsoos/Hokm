
namespace Hokm.Infrastructure.Services.Redis.Constants
{
    public class RedisCacheKeySchema
    {
        public const string User = "user";
        public const string Game = "game:";
        public const string Player = "player:";
        public const string Round = "round:";
        public const string Team = "team:";
        public const string Trick = "trick:";

        public static string UserKey(Guid userId) => $"{User}{userId}";
        public static string GameKey(Guid gameId) => $"{Game}{gameId}";
        public static string PlayerKey(Guid userId) => $"{Player}{userId}";
        public static string RoundKey(Guid roundId) => $"{Round}{roundId}";
        public static string TeamKey(Guid teamId) => $"{Team}{teamId}";
        public static string TrickKey(Guid trickId) => $"{Trick}{trickId}";

    }
}
