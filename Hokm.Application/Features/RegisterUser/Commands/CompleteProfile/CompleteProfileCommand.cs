using ErrorOr;
using Hokm.Application.DTOs.Auth;
using MediatR;

namespace Hokm.Application.Features.RegisterUser.Commands.CompleteProfile
{
    public class CompleteProfileCommand : IRequest<ErrorOr<AuthResponse>>
    {
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string FullName { get; set; }
        public int Avatar { get; set; }
    }
}
