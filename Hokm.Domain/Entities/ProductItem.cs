using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hokm.Domain.Entities
{
    public class ProductItem : BaseEntity
    {
        public Guid ProductId { get; private set; }

        public string? Content { get; private set; }

        public Guid? LinkedProductId { get; private set; }

        public int Quantity { get; private set; }

        public int SortOrder { get; private set; }

        public bool IsActive { get; private set; }

        public Product Product { get; private set; }
        public Product? LinkedProduct { get; private set; }

        public static ProductItem CreateForPack(Guid productId, string content, int sortOrder = 0)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("محتوای آیتم پک نمی‌تواند خالی باشد.", nameof(content));

            return new ProductItem(productId, content, null, 1, sortOrder);
        }

        public static ProductItem CreateForBundle(Guid productId, Guid linkedProductId, int quantity = 1, int sortOrder = 0)
        {
            if (productId == linkedProductId)
                throw new ArgumentException("محصول نمی‌تواند خودش را به عنوان زیرمجموعه باندل داشته باشد.");

            if (quantity <= 0)
                throw new ArgumentException("تعداد آیتم باندل باید بزرگتر از صفر باشد.", nameof(quantity));

            return new ProductItem(productId, null, linkedProductId, quantity, sortOrder);
        }

        private ProductItem(
            Guid productId,
            string? content,
            Guid? linkedProductId,
            int quantity,
            int sortOrder) : base()
        {
            ProductId = productId;
            Content = content;
            LinkedProductId = linkedProductId;
            Quantity = quantity;
            SortOrder = sortOrder;
            IsActive = true;
        }

        public void UpdateSortOrder(int newSortOrder)
        {
            SortOrder = newSortOrder;
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
        public ProductItem() { }
    }
}
