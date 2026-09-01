using ErrorOr;
using Hokm.Application.DTOs.Payment;
using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.InitiatePayment.Commands
{
    public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, ErrorOr<InitiatePaymentResultDto>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IProductRepository _productRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IDirectPaymentService _directPaymentService;

        public InitiatePaymentCommandHandler(
            IUserRepository userRepository,
            IProductRepository productRepository,
            ITransactionRepository transactionRepository,
            IDirectPaymentService directPaymentService)
        {
            _userRepository = userRepository;
            _productRepository = productRepository;
            _transactionRepository = transactionRepository;
            _directPaymentService = directPaymentService;
        }

        public async Task<ErrorOr<InitiatePaymentResultDto>> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
        {
            if (request.Gateway == GatewayType.CafeBazaar || request.Gateway == GatewayType.Myket)
                return Error.Validation("Payment.InvalidGateway", "برای خریدهای مارکت اندروید باید از سیستم تایید رسید استفاده کنید.");

            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) return Error.NotFound("User.NotFound", "کاربر یافت نشد.");

            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null) return Error.NotFound("Product.NotFound", "محصول یافت نشد.");

            if (!product.IsActive) return Error.Validation("Product.Inactive", "این محصول غیرفعال است.");
            if (product.PaymentType != PaymentType.DirectPayment) return Error.Validation("Product.InvalidPayment", "این محصول با درگاه مستقیم قابل خرید نیست.");

            var invoiceNumber = $"INV-{DateTime.UtcNow.Ticks}-{request.UserId.ToString().Substring(0, 4)}";
            var callbackUrl = "https://api.mygame.com/api/payment/callback";

            try
            {
                var paymentUrl = await _directPaymentService.RequestPaymentUrlAsync(
                    invoiceNumber,
                    (decimal)product.Price,
                    product.Title,
                    callbackUrl
                );

                var transaction = new Transaction(
                    userId: user.Id,
                    productId: product.Id,
                    amount: (decimal)product.Price,
                    gateway: request.Gateway,
                    invoiceNumber: invoiceNumber,
                    paymentToken: paymentUrl
                );

                await _transactionRepository.CreateAsync(transaction, cancellationToken);

                return new InitiatePaymentResultDto(
                    Success: true,
                    TransactionId: transaction.Id,
                    PaymentUrlOrToken: paymentUrl,
                    Message: "لینک پرداخت مستقیم صادر شد."
                );
            }
            catch (Exception ex)
            {
                return Error.Failure("Payment.GatewayError", $"خطا در اتصال به بانک: {ex.Message}");
            }
        }
    }
}
