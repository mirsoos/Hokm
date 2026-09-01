
namespace Hokm.Domain.Enums
{
    public enum TransactionStatus
    {
        Pending = 1,   // در انتظار پرداخت کاربر
        Completed = 2, // پرداخت موفق و تایید شده توسط سرور
        Failed = 3     // تراکنش ناموفق یا لغو شده
    }
}
