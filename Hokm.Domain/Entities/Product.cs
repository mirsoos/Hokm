using System;
using System.Collections.Generic;
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
        public string? MarketSku { get; private set; }
        public bool IsFree =>
            PaymentType == PaymentType.Free ||
            (Price == 0 && PaymentType != PaymentType.MarketIap);

        public List<ProductItem> Items { get; private set; } = new();

        public Product(
            string title,
            string description,
            string assetKey,
            ProductType productType,
            PaymentType paymentType,
            long price,
            int? coinAmount = null,
            int? vipDurationDays = null,
            string? marketSku = null) : base()
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("عنوان محصول نمی‌تواند خالی باشد.", nameof(title));
            if (string.IsNullOrWhiteSpace(assetKey) &&
                productType != ProductType.VipSubscription &&
                productType != ProductType.CoinBundle &&
                productType != ProductType.QuickChat)
            {
                throw new ArgumentException("کلید دارایی (AssetKey) برای این نوع محصول الزامی است.", nameof(assetKey));
            }

            if (paymentType == PaymentType.MarketIap && string.IsNullOrWhiteSpace(marketSku))
                throw new ArgumentException("برای محصولات درون‌برنامه‌ای، MarketSku الزامی است.", nameof(marketSku));

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
            MarketSku = marketSku;
            IsActive = true;
        }

        public ProductItem AddPackItem(string content, int sortOrder = 0)
        {
            var item = ProductItem.CreateForPack(Id, content, sortOrder);
            Items.Add(item);
            IncrementVersion();
            return item;
        }

        public ProductItem AddBundleItem(Guid linkedProductId, int quantity = 1, int sortOrder = 0)
        {
            var item = ProductItem.CreateForBundle(Id, linkedProductId, quantity, sortOrder);
            Items.Add(item);
            IncrementVersion();
            return item;
        }

        public void RemoveItem(Guid itemId)
        {
            var item = Items.Find(i => i.Id == itemId);
            if (item != null)
            {
                Items.Remove(item);
                IncrementVersion();
            }
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