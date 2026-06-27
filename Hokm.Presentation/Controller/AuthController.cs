using Hokm.Application.Features.RegisterUser.Commands.SendVerificationCode;
using Hokm.Application.Features.RegisterUser.Commands.VerifyCode;
using Hokm.Application.Features.RegisterUser.Commands.CompleteProfile;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Hokm.Presentation.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("send-code")]
        public async Task<IActionResult> SendCode(SendVerificationCodeCommand request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(request, cancellationToken);

            return result.Match<IActionResult>(
                success => Ok(success),
                errors => BadRequest(new
                {
                    Success = false,
                    Message = errors.First().Description
                })
            );
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode(VerifyCodeCommand request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(request, cancellationToken);

            return result.Match<IActionResult>(
                success => Ok(success),
                errors => BadRequest(new
                {
                    Success = false,
                    Message = errors.First().Description
                })
            );
        }

        [HttpPost("complete-profile")]
        public async Task<IActionResult> CompleteProfile(CompleteProfileCommand request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(request, cancellationToken);

            return result.Match<IActionResult>(
                success => Ok(success),
                errors => BadRequest(new
                {
                    Success = false,
                    Message = errors.First().Description
                })
            );
        }
    }
}