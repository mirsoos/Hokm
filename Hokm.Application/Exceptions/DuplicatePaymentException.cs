namespace Hokm.Application.Exceptions
{
    public class DuplicatePaymentException : Exception
    {
        public string? PaymentToken { get; }

        public DuplicatePaymentException(string? paymentToken)
            : base("This payment token has already been used.")
        {
            PaymentToken = paymentToken;
        }
    }
}
