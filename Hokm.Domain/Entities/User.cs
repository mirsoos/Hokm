
namespace Hokm.Domain.Entities
{
    public class User : BaseEntity
    {
        public string FullName { get; private set; }
        public string UserName { get; private set; }
        public string? PhoneNumber { get; private set; }
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

        public User(string fullname , string email, string phoneNumber , string userName)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber) && string.IsNullOrWhiteSpace(email))
                throw new ArgumentNullException("PhoneNumber or Email Required.");
            Email = email;
            FullName = fullname;
            PhoneNumber = phoneNumber;
            UserName = userName;
        }

        public void SetUserToken(string refreshToken)
        {
            RefreshToken = refreshToken;
            TokenExpireDate = DateTime.UtcNow;
        }
    }
}
