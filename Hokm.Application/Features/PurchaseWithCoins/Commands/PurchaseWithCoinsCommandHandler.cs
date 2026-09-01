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
            // ۱. لود کردن کاربر
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                return Error.NotFound("User.NotFound", "کاربر مورد نظر یافت نشد.");

            // ۲. لود کردن محصول
            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null)
                return Error.NotFound("Product.NotFound", "محصول مورد نظر یافت نشد.");

            // ۳. بررسی فعال بودن محصول در فروشگاه
            if (!product.IsActive)
                return Error.Validation("Product.Inactive", "این محصول در حال حاضر غیرفعال است.");

            // ۴. بررسی اینکه روش پرداخت محصول حتماً سکه‌ای باشد
            if (product.PaymentType != PaymentType.Coins)
                return Error.Validation("Product.InvalidPaymentType", "این محصول با سکه قابل خرید نیست.");

            // ۵. بررسی عدم تکراری بودن خرید برای آیتم‌های غیرمصرفی (تم، آواتار و قاب)
            if (product.ProductType != ProductType.XpBooster && user.OwnedProductIds.Contains(product.Id))
                return Error.Conflict("Product.AlreadyOwned", "شما قبلاً این محصول را خریداری کرده‌اید.");

            // ۶. بررسی کافی بودن موجودی سکه کاربر و کسر آن
            if (user.Coin < product.Price)
                return Error.Validation("User.InsufficientCoins", "موجودی سکه شما کافی نیست.");

            user.DeductCoins(product.Price);

            // ۷. تحویل محصول بر اساس نوع آن
            if (product.ProductType == ProductType.VipSubscription)
            {
                // فعال‌سازی یا تمدید اشتراک VIP
                user.ActivateVip(product.VipDurationDays ?? 0);
            }
            else
            {
                // اضافه کردن به لیست دارایی‌های دائمی
                user.AddProductToInventory(product.Id);
            }

            // ۸. ذخیره تغییرات در دیتابیس مونگو
            await _userRepository.UpdateAsync(user, cancellationToken);

            return new PurchaseWithCoinsResultDto(
                Success: true,
                Message: "خرید با موفقیت انجام شد.",
                RemainingCoins: user.Coin
            );
        }
    }
}
