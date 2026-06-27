using ErrorOr;
using MediatR;

namespace Hokm.Application.Features.Profile.Commands.DeleteUser
{
    public record DeleteUserCommand(Guid UserId) : IRequest<ErrorOr<bool>>;
}
