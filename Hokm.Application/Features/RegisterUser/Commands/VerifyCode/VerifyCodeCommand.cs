using ErrorOr;
using Hokm.Application.DTOs.Auth;
using MediatR;

namespace Hokm.Application.Features.RegisterUser.Commands.VerifyCode
{
    public record class VerifyCodeCommand : IRequest<ErrorOr<VerifyCodeResponse>>
    {
        public string Code { get; set; }
        public string PhoneNumber { get; set; }
    }
}
