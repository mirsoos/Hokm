using ErrorOr;
using MediatR;

namespace Hokm.Application.Features.profile.Commands.UpdateProfile
{
	public record UpdateProfileCommand : IRequest<ErrorOr<UpdateProfileResponse>>
	{
		public Guid UserId { get; set; }
		public string FullName { get; init; }
        public int AvatarRef { get; set; }
	}

	public record UpdateProfileResponse(bool Success, string Message);
}
