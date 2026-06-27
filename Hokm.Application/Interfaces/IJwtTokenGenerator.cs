
namespace Hokm.Application.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(Guid id, string phoneNumber, string userName);
    }
}
