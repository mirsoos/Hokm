using ErrorOr;
using Hokm.Application.DTOs.Auth;
using MediatR;

namespace Hokm.Application.Features.RegisterUser.Commands.SendVerificationCode
{
    public record SendVerificationCodeCommand : IRequest<ErrorOr<SendCodeResponse>>
    {
        public string PhoneNumber { get; set; }
    }
}
