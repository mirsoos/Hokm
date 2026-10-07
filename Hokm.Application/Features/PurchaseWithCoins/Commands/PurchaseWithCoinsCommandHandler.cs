using System.Linq;
using ErrorOr;
using Hokm.Application.DTOs.Payment;
using Hokm.Application.Interfaces;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.PurchaseWithCoins.Commands
{
    public class PurchaseWithCoinsCommandHandler : IRequestHandler<PurchaseWithCoinsCommand, ErrorOr<PurchaseWithCoinsResultDto>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IProductRepository _productRepository;

        public PurchaseWithCoinsCommandHandler(
            IUserRepository userRepository,
            IProductRepository productRepository)
        {
            _userRepository = userRepository;
            _productRepository = productRepository;
        }

        public async Task<ErrorOr<PurchaseWithCoinsResultDto>> Handle(PurchaseWithCoinsCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                return Error.NotFound("User.NotFound", "کاربر مورد نظر یافت نشد.");

            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null)
                return Error.NotFound("Product.NotFound", "محصول مورد نظر یافت نشد.");

            if (!product.IsActive)
                return Error.Validation("Product.Inactive", "این محصول در حال حاضر غیرفعال است.");

            if (product.PaymentType != PaymentType.Coins)
                return Error.Validation("Product.InvalidPaymentType", "این محصول با سکه قابل خرید نیست.");

            if (product.ProductType != ProductType.XpBooster && user.OwnedProductIds.Contains(product.Id))
                return Error.Conflict("Product.AlreadyOwned", "شما قبلاً این محصول را خریداری کرده‌اید.");

            if (user.Coin < product.Price)
                return Error.Validation("User.InsufficientCoins", "موجودی سکه شما کافی نیست.");

            user.DeductCoins(product.Price);

            if (product.ProductType == ProductType.VipSubscription)
            {
                user.ActivateVip(product.VipDurationDays ?? 0);
            }
            else
            {
                user.AddProductToInventory(product.Id);

                if (product.Items != null && product.Items.Any())
                {
                    foreach (var item in product.Items.Where(i => i.IsActive && i.LinkedProductId.HasValue))
                    {
                        user.AddProductToInventory(item.LinkedProductId!.Value);
                    }
                }

                if (product.CoinAmount.HasValue && product.CoinAmount.Value > 0)
                {
                    user.AddCoins(product.CoinAmount.Value);
                }

                if (product.VipDurationDays.HasValue && product.VipDurationDays.Value > 0)
                {
                    user.ActivateVip(product.VipDurationDays.Value);
                }
            }

            await _userRepository.UpdateAsync(user, cancellationToken);

            return new PurchaseWithCoinsResultDto(
                Success: true,
                Message: "خرید با موفقیت انجام شد.",
                RemainingCoins: user.Coin
            );
        }
    }
}