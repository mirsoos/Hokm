
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
        public int Score { get; private set; } = 0;
        public int Level { get; private set; } = 1;
        public int Wins { get; private set; } = 0;
        public int Loses { get; private set; } = 0;
        public int TotalGames => Wins + Loses;
        public long Coin { get; private set; } = 1000;
        public bool IsBot { get; set; } = false;

        public User(string fullname , string? email, string phoneNumber , string userName)
        {
            Email = email;
            FullName = fullname;
            PhoneNumber = phoneNumber;
            UserName = userName;
        }

        public void UpdateFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentNullException(nameof(fullName));

            FullName = fullName;
        }

        public void DeductCoins(int amount)
        {
            if (Coin < amount)
                throw new InvalidOperationException("موجودی سکه کافی نیست.");
            Coin -= amount;
        }

        public void SetUserToken(string refreshToken)
        {
            RefreshToken = refreshToken;
            TokenExpireDate = DateTime.UtcNow;
        }
        public User() { }
    }
}
