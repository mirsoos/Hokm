namespace Hokm.Application.DTOs.Payment
{
    public record InitiatePaymentResultDto(
        bool Success,
        Guid TransactionId,
        string PaymentUrlOrToken,
        string Message
    );
}
