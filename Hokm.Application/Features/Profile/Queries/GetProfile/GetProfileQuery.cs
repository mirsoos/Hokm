using ErrorOr;
using MediatR;

namespace Hokm.Application.Features.profile.Queries.GetProfile
{
	public record GetProfileQuery(Guid UserId) : IRequest<ErrorOr<GetProfileResponse>>;

    public record GetProfileResponse(
            Guid Id,
            string UserName,
            string FullName,
            string? Email,
            string PhoneNumber,
            int AvatarRef,
            int Score,
            int Level,
            int Wins,
            int Loses,
            int TotalGames,
            long Coin,
            bool IsVip,
            DateTime? VipExpireDate,
            bool HasChangedName,
            List<Guid> OwnedProductIds,
            Guid? ActiveCardThemeId,
            Guid? ActiveTableThemeId,
            Guid? ActiveAvatarBorderId
        );
}
