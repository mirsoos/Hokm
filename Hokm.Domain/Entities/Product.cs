using System;
using System.Text.Json.Serialization;
using Hokm.Domain.Enums;

namespace Hokm.Domain.Entities
{
    public class Product : BaseEntity
    {
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string AssetKey { get; private set; }
        public ProductType ProductType { get; private set; }
        public PaymentType PaymentType { get; private set; }
        public long Price { get; private set; }
        public int? CoinAmount { get; private set; }
        public int? VipDurationDays { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsFree => PaymentType == PaymentType.Free || Price == 0;
        public Product(
            string title,
            string description,
            string assetKey,
            ProductType productType,
            PaymentType paymentType,
            long price,
            int? coinAmount = null,
            int? vipDurationDays = null) : base()
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("عنوان محصول نمی‌تواند خالی باشد.", nameof(title));
            if (string.IsNullOrWhiteSpace(assetKey) &&
                productType != ProductType.VipSubscription &&
                productType != ProductType.CoinBundle)
            {
                throw new ArgumentException("کلید دارایی (AssetKey) برای این نوع محصول الزامی است.", nameof(assetKey));
            }

            if (price < 0)
                throw new ArgumentException("قیمت محصول نمی‌تواند عدد منفی باشد.", nameof(price));

            Title = title;
            Description = description;
            AssetKey = assetKey;
            ProductType = productType;
            PaymentType = paymentType;
            Price = price;
            CoinAmount = coinAmount;
            VipDurationDays = vipDurationDays;
            IsActive = true;
        }

        public void UpdatePrice(long newPrice)
        {
            if (newPrice < 0)
                throw new ArgumentException("قیمت جدید نمی‌تواند منفی باشد.");

            Price = newPrice;
            IncrementVersion();
        }

        public void Deactivate()
        {
            IsActive = false;
            IncrementVersion();
        }

        public void Activate()
        {
            IsActive = true;
            IncrementVersion();
        }

        [JsonConstructor]
        public Product() { }
    }
}