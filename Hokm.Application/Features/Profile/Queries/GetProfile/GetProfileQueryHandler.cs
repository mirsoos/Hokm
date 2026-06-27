using ErrorOr;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.profile.Queries.GetProfile
{
    public class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, ErrorOr<GetProfileResponse>>
    {
        private readonly IUserRepository _userRepository;
        public GetProfileQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ErrorOr<GetProfileResponse>> Handle(GetProfileQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if(user == null)
				return Error.NotFound("User.NotFound", "User not Found.");

			return new GetProfileResponse(
				user.Id,
				user.UserName,
				user.FullName,
				user.Email,
				user.PhoneNumber,
				user.AvatarRef,
				user.Score,
				user.Level,
				user.Wins,
				user.Loses,
				user.TotalGames,
				user.Coin
			);
		}
    }
}
