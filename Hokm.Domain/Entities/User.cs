using System.Text.Json.Serialization;

namespace Hokm.Domain.Entities
{
    public class User : BaseEntity
    {
        public string FullName { get; private set; }
        public string UserName { get; private set; }
        public string PhoneNumber { get; private set; }
        public string? Email { get; private set; }
        public string? RefreshToken { get; private set; }
        public DateTime? TokenExpireDate { get; private set; }
        public int AvatarRef { get; private set; } = 1;
        public int Score { get; private set; } = 0; // همان مجموع امتیاز XP کاربر
        public int Level { get; private set; } = 1;
        public int Wins { get; private set; } = 0;
        public int Loses { get; private set; } = 0;
        public int TotalGames => Wins + Loses;
        public long Coin { get; private set; } = 1000;
        public bool IsBot { get; set; } = false;
        public DateTime? VipExpireDate { get; private set; }
        public bool IsVip => VipExpireDate.HasValue && VipExpireDate.Value > DateTime.UtcNow;
        public List<Guid> OwnedProductIds { get; private set; } = new List<Guid>();

        public Guid? ActiveCardThemeId { get; private set; }
        public Guid? ActiveTableThemeId { get; private set; }
        public Guid? ActiveAvatarBorderId { get; private set; }
        public Guid? ActiveAvatarId { get; private set; }

        public bool HasChangedName { get; private set; } = false;

        public User(string fullname, string? email, string phoneNumber, string userName) : base()
        {
            if (string.IsNullOrWhiteSpace(fullname))
                throw new ArgumentNullException(nameof(fullname));
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentNullException(nameof(userName));

            Email = email;
            FullName = fullname;
            PhoneNumber = phoneNumber;
            UserName = userName;

            OwnedProductIds = new List<Guid>();
        }

        public void RecordWin(int xpReward)
        {
            Wins++;
            AddXp(xpReward);
        }

        public void RecordLoss(int xpReward)
        {
            Loses++;
            AddXp(xpReward);
        }

        private void AddXp(int xpAmount)
        {
            if (xpAmount <= 0) return;

            Score += xpAmount;

            int newLevel = 1;
            double requiredForNext = 100.0;
            double accumulatedXp = 0.0;

            while (Score >= accumulatedXp + requiredForNext)
            {
                accumulatedXp += requiredForNext;
                newLevel++;
                requiredForNext *= 1.25;
            }

            if (newLevel > Level)
            {
                Level = newLevel;
            }

            IncrementVersion();
        }

        public void UpdateFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentNullException(nameof(fullName));

            FullName = fullName;
            IncrementVersion();
        }

        public void ChangeUserName(string newUserName)
        {
            if (string.IsNullOrWhiteSpace(newUserName))
                throw new ArgumentException("نام کاربری جدید نمی‌تواند خالی باشد.");

            UserName = newUserName;
            HasChangedName = true;
            IncrementVersion();
        }

        public void AddCoins(long amount)
        {
            if (amount <= 0)
                throw new ArgumentException("مقدار سکه افزایشی باید بزرگتر از صفر باشد.");

            Coin += amount;
            IncrementVersion();
        }

        public void DeductCoins(long amount)
        {
            if (Coin < amount)
                throw new InvalidOperationException("موجودی سکه کافی نیست.");

            Coin -= amount;
            IncrementVersion();
        }

        public void ActivateVip(int days)
        {
            if (days <= 0)
                throw new ArgumentException("تعداد روزهای اشتراک باید معتبر باشد.");

            if (IsVip)
            {
                VipExpireDate = VipExpireDate.Value.AddDays(days);
            }
            else
            {
                VipExpireDate = DateTime.UtcNow.AddDays(days);
            }

            IncrementVersion();
        }

        public void AddProductToInventory(Guid productId)
        {
            if (productId == Guid.Empty)
                throw new ArgumentException("شناسه محصول نامعتبر است.");

            if (!OwnedProductIds.Contains(productId))
            {
                OwnedProductIds.Add(productId);
                IncrementVersion();
            }
        }

        public void EquipCardTheme(Guid? cardThemeId, bool isProductFree)
        {
            if (cardThemeId.HasValue && !isProductFree && !OwnedProductIds.Contains(cardThemeId.Value))
                throw new InvalidOperationException("شما مالک این تم کارت نیستید.");

            ActiveCardThemeId = cardThemeId;
            IncrementVersion();
        }

        public void EquipTableTheme(Guid? tableThemeId, bool isProductFree)
        {
            if (tableThemeId.HasValue && !isProductFree && !OwnedProductIds.Contains(tableThemeId.Value))
                throw new InvalidOperationException("شما مالک این تم میز نیستید.");

            ActiveTableThemeId = tableThemeId;
            IncrementVersion();
        }

        public void EquipAvatarBorder(Guid? borderId, bool isProductFree)
        {
            if (borderId.HasValue && !isProductFree && !OwnedProductIds.Contains(borderId.Value))
                throw new InvalidOperationException("شما مالک این قاب آواتار نیستید.");

            ActiveAvatarBorderId = borderId;
            IncrementVersion();
        }

        public void EquipAvatar(Guid? avatarId, bool isProductFree)
        {
            if (avatarId.HasValue && !isProductFree && !OwnedProductIds.Contains(avatarId.Value))
                throw new InvalidOperationException("شما مالک این آواتار نیستید.");

            ActiveAvatarId = avatarId;
            IncrementVersion();
        }

        public void SetAvatarRef(int avatarRef)
        {
            if (avatarRef <= 0)
                throw new ArgumentException("شناسه آواتار باید معتبر باشد.");

            AvatarRef = avatarRef;
            IncrementVersion();
        }

        public void SetUserToken(string refreshToken)
        {
            RefreshToken = refreshToken;
            TokenExpireDate = DateTime.UtcNow;
            IncrementVersion();
        }

        [JsonConstructor]
        public User() { }
    }
}