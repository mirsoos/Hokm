using ErrorOr;
using Hokm.Application.DTOs;
using Hokm.Application.Interfaces;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.EquipProduct.Commands
{
    public class EquipProductCommandHandler : IRequestHandler<EquipProductCommand, ErrorOr<EquipProductResultDto>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IProductRepository _productRepository;

        public EquipProductCommandHandler(
            IUserRepository userRepository,
            IProductRepository productRepository)
        {
            _userRepository = userRepository;
            _productRepository = productRepository;
        }

        private static int ExtractAvatarRef(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey)) return 1;

            var match = System.Text.RegularExpressions.Regex.Match(assetKey, @"(\d+)\.png$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int refNum))
            {
                return refNum;
            }
            return 1;
        }

        public async Task<ErrorOr<EquipProductResultDto>> Handle(EquipProductCommand request, CancellationToken cancellationToken)
        {
            // ۱. لود کردن کاربر
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                return Error.NotFound("User.NotFound", "کاربر مورد نظر یافت نشد.");

            // ۲. لود کردن محصول از کاتالوگ فروشگاه
            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null)
                return Error.NotFound("Product.NotFound", "محصول مورد نظر در فروشگاه یافت نشد.");

            // ۳. بررسی همخوانی نوع محصول درخواست شده با دیتابیس (جهت جلوگیری از درخواست‌های فیک)
            if (product.ProductType != request.ProductType)
                return Error.Validation("Product.TypeMismatch", "نوع محصول با نوع آیتم درخواستی همخوانی ندارد.");

            // ۴. اعمال منطق فعال‌سازی بر اساس نوع محصول تزئینی
            try
            {
                switch (product.ProductType)
                {
                    case ProductType.CardTheme:
                        user.EquipCardTheme(product.Id, product.IsFree);
                        break;

                    case ProductType.TableTheme:
                        user.EquipTableTheme(product.Id, product.IsFree);
                        break;

                    case ProductType.AvatarBorder:
                        user.EquipAvatarBorder(product.Id, product.IsFree);
                        break;

                    case ProductType.Avatar:
                        int avatarRef = ExtractAvatarRef(product.AssetKey);
                        user.EquipAvatar(product.Id, product.IsFree);
                        user.SetAvatarRef(avatarRef);
                        break;

                    default:
                        return Error.Validation("Product.NotEquippable", "این نوع محصول قابلیت فعال‌سازی یا تجهیز روی پروفایل را ندارد.");
                }

                await _userRepository.UpdateAsync(user, cancellationToken);

                return new EquipProductResultDto(
                    Success: true,
                    Message: $"{product.Title} با موفقیت روی پروفایل شما فعال شد."
                );
            }
            catch (InvalidOperationException ex)
            {
                return Error.Validation("User.NotOwned", ex.Message);
            }
        }
    }
}
