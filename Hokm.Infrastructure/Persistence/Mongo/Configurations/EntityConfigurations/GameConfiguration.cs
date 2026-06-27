using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Reflection; // اضافه شد برای تعریف متغیرهای رفلکشن صندلی‌ها

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations.EntityConfigurations
{
    public class GameConfiguration : IEntityConfiguration
    {
        private static bool _isConfigured = false;
        public void Configure()
        {
            if (_isConfigured) return;

            if (!BsonClassMap.IsClassMapRegistered(typeof(Game)))
            {
                BsonClassMap.RegisterClassMap<Game>(cm =>
                {
                    cm.AutoMap();
                    cm.MapMember(c => c.Status).SetSerializer(new EnumSerializer<GameStatus>(BsonType.String));

                    // فعال‌سازی سِتِرهای خصوصی (private set) از طریق لغو مپ خواندنی پیش‌فرض و ثبت مجدد آن به صورت عضو قابل ویرایش

                    var playersProp = typeof(Game).GetProperty(nameof(Game.Players))!;
                    cm.UnmapMember(playersProp);
                    cm.MapMember(playersProp);

                    var teamsProp = typeof(Game).GetProperty(nameof(Game.Teams))!;
                    cm.UnmapMember(teamsProp);
                    cm.MapMember(teamsProp);

                    var roundsProp = typeof(Game).GetProperty(nameof(Game.Rounds))!;
                    cm.UnmapMember(roundsProp);
                    cm.MapMember(roundsProp);

                    var winnerPlayersProp = typeof(Game).GetProperty(nameof(Game.WinnerPlayers))!;
                    cm.UnmapMember(winnerPlayersProp);
                    cm.MapMember(winnerPlayersProp);

                    cm.SetIgnoreExtraElements(true);
                });
            }
            _isConfigured = true;
        }
    }
}