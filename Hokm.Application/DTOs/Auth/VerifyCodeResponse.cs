
namespace Hokm.Application.DTOs.Auth
{
    public class VerifyCodeResponse
    {
        public string Message { get; set; }
        public bool IsNewUser { get; set; }
        public string? Token { get; set; }
    }
}
