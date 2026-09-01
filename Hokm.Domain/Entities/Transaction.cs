using System.Text.Json.Serialization;
using Hokm.Domain.Enums;

namespace Hokm.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        public Guid UserId { get; private set; }
        public Guid ProductId { get; private set; }
        public decimal Amount { get; private set; }
        public GatewayType Gateway { get; private set; }
        public TransactionStatus Status { get; private set; } = TransactionStatus.Pending;
        public string InvoiceNumber { get; private set; }
        public string? PaymentToken { get; private set; }
        public string? ReferenceId { get; private set; }
        public DateTime? VerifyDate { get; private set; }
        public string? FailureReason { get; private set; }

        public Transaction(
            Guid userId,
            Guid productId,
            decimal amount,
            GatewayType gateway,
            string invoiceNumber,
            string? paymentToken = null) : base()
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("شناسه کاربر نامعتبر است.", nameof(userId));
            if (productId == Guid.Empty)
                throw new ArgumentException("شناسه محصول نامعتبر است.", nameof(productId));
            if (amount <= 0)
                throw new ArgumentException("مبلغ تراکنش باید بزرگتر از صفر باشد.", nameof(amount));
            if (gateway == GatewayType.None)
                throw new ArgumentException("درگاه پرداخت معتبری انتخاب نشده است.", nameof(gateway));
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                throw new ArgumentException("شماره فاکتور نمی‌تواند خالی باشد.", nameof(invoiceNumber));

            UserId = userId;
            ProductId = productId;
            Amount = amount;
            Gateway = gateway;
            InvoiceNumber = invoiceNumber;
            PaymentToken = paymentToken;
            Status = TransactionStatus.Pending;
        }
        public void Complete(string referenceId)
        {
            if (Status != TransactionStatus.Pending)
                throw new InvalidOperationException("تنها تراکنش‌های در انتظار پرداخت، قابل تکمیل هستند.");
            if (string.IsNullOrWhiteSpace(referenceId))
                throw new ArgumentException("کد پیگیری تراکنش معتبر نیست.", nameof(referenceId));

            Status = TransactionStatus.Completed;
            ReferenceId = referenceId;
            VerifyDate = DateTime.UtcNow;
            IncrementVersion();
        }
        public void Fail(string reason)
        {
            if (Status != TransactionStatus.Pending)
                throw new InvalidOperationException("تنها تراکنش‌های در انتظار پرداخت، می‌توانند لغو شوند.");

            Status = TransactionStatus.Failed;
            FailureReason = reason;
            IncrementVersion();
        }

        [JsonConstructor]
        protected Transaction() { }
    }
}