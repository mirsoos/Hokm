using ErrorOr;
using Hokm.Application.DTOs.Payment;
using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hokm.Application.Features.VerifyStorePurchase.Commands
{
    public class VerifyStorePurchaseCommandHandler : IRequestHandler<VerifyStorePurchaseCommand, ErrorOr<VerifyStorePurchaseResultDto>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IProductRepository _productRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IStorePaymentService _storePaymentService;

        public VerifyStorePurchaseCommandHandler(
            IUserRepository userRepository,
            IProductRepository productRepository,
            ITransactionRepository transactionRepository,
            IStorePaymentService storePaymentService)
        {
            _userRepository = userRepository;
            _productRepository = productRepository;
            _transactionRepository = transactionRepository;
            _storePaymentService = storePaymentService;
        }

        public async Task<ErrorOr<VerifyStorePurchaseResultDto>> Handle(VerifyStorePurchaseCommand request, CancellationToken cancellationToken)
        {
            if (request.Gateway != GatewayType.CafeBazaar && request.Gateway != GatewayType.Myket)
                return Error.Validation("Payment.InvalidGateway", "درگاه نامعتبر برای خرید درون‌برنامه‌ای.");

            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) return Error.NotFound("User.NotFound", "کاربر یافت نشد.");

            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null) return Error.NotFound("Product.NotFound", "محصول یافت نشد.");

            bool isVerified = await _storePaymentService.VerifyStorePurchaseAsync(
                marketProductId: product.AssetKey,
                purchaseToken: request.PurchaseToken,
                gateway: request.Gateway
            );

            if (!isVerified)
                return Error.Validation("Payment.VerificationFailed", "رسید خرید توسط مارکت تایید نشد.");

            var invoiceNumber = $"IAP-{DateTime.UtcNow.Ticks}";
            var transaction = new Transaction(
                userId: user.Id,
                productId: product.Id,
                amount: (decimal)product.Price,
                gateway: request.Gateway,
                invoiceNumber: invoiceNumber,
                paymentToken: request.PurchaseToken
            );

            transaction.Complete(request.PurchaseToken);
            await _transactionRepository.CreateAsync(transaction, cancellationToken);

            if (product.ProductType == ProductType.VipSubscription)
            {
                user.ActivateVip(product.VipDurationDays ?? 0);
            }
            else if (product.ProductType == ProductType.CoinBundle)
            {
                user.AddCoins(product.CoinAmount ?? 0);
            }
            else
            {
                user.AddProductToInventory(product.Id);
            }

            await _userRepository.UpdateAsync(user, cancellationToken);

            return new VerifyStorePurchaseResultDto(
                Success: true,
                Message: "خرید درون‌برنامه‌ای با موفقیت تایید و تحویل داده شد."
            );
        }
    }
}
