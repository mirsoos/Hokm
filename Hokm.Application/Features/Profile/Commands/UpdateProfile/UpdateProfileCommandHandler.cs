using ErrorOr;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.profile.Commands.UpdateProfile
{
    public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, ErrorOr<UpdateProfileResponse>>
    {
        private readonly IUserRepository _userRepository;
        public UpdateProfileCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ErrorOr<UpdateProfileResponse>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
        {
			var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
			if (user == null)
				return Error.NotFound("User.NotFound", "User not Found.");
			user.UpdateFullName(request.FullName);

			await _userRepository.UpdateProfileAsync(user.Id,request.FullName,request.AvatarRef, cancellationToken);

			return new UpdateProfileResponse(true, "پروفایل با موفقیت بروزرسانی شد");
		}
    }
}
