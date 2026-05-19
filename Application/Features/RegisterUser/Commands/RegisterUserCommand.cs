using MediatR;

namespace Hokm.Application.Features.RegisterUser.Commands
{
    public class RegisterUserCommand : IRequest<Guid>
    {
        public string FullName { get; set; }
        public string UserName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
    }
}
