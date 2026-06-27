
namespace Hokm.Application.DTOs.Auth
{
    public class AuthResponse
    {
        public Guid UserId { get; set; }
        public string Token { get; set; }
        public string UserName { get; set; }
    }
}
