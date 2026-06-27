using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using System;
using System.Reflection; // اضافه شد برای فیلتر کردن هوشمند صندلی‌ها

namespace Hokm.Infrastructure.Persistence.Mongo.Configurations
{
    public static class MongoDbConventionRegistry
    {
        private static bool _isConfigured = false;

        public static void Configure()
        {
            if (_isConfigured) return;

            var conventionPack = new ConventionPack
            {
                new IgnoreExtraElementsConvention(true),
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreIfNullConvention(true),
                new ImmutableTypeClassMapConvention(),
                new PrivateSetterConvention() // ۱. ثبت قرارداد سراسری فعال‌سازی خودکار سِتِرهای خصوصی
            };

            ConventionRegistry.Register(
                "HokmConventions",
                conventionPack,
                t => t.Namespace != null && t.Namespace.StartsWith("Hokm.Domain"));

            _isConfigured = true;
        }
    }

    // ۲. تعریف کلاس قرارداد عمومی جهت اسکن هوشمندانه و بدون خطای تمام ویژگی‌های با سِتِر غیرعمومی در کل لایه دامنه
    public class PrivateSetterConvention : ConventionBase, IClassMapConvention
    {
        public void Apply(BsonClassMap classMap)
        {
            var properties = classMap.ClassType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var setMethod = prop.GetSetMethod(nonPublic: true);

                // اگر پروپرتی دارای سِتِری به جز دسترسی عمومی (مانند private set) باشد
                if (setMethod != null && !setMethod.IsPublic)
                {
                    // لغو مپ خواندنی پیش‌فرض مونوگو و ثبت مجدد رسمی عضو جهت فعال‌سازی خودکار متد نوشتن
                    classMap.UnmapMember(prop);
                    classMap.MapMember(prop);
                }
            }
        }
    }
}