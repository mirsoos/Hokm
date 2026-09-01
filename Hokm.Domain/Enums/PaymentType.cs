
namespace Hokm.Domain.Enums
{
    public enum PaymentType
    {
        Free = 1,          // رایگان / پیش‌فرض
        Coins = 2,         // پرداخت با سکه درون‌برنامه‌ای
        DirectPayment = 3, // پرداخت مستقیم ریالی (درگاه یا کافه‌بازار)
        Ads = 4            // دریافت در ازای تماشای تبلیغات
    }
}
