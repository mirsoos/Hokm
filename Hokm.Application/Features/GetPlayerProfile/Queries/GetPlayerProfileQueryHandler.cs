using ErrorOr;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.GetPlayerProfile.Queries
{
    public class GetPlayerProfileQueryHandler
        : IRequestHandler<GetPlayerProfileQuery, ErrorOr<GetPlayerProfileResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IProductRepository _productRepository;

        public GetPlayerProfileQueryHandler(
            IUserRepository userRepository,
            IProductRepository productRepository)
        {
            _userRepository = userRepository;
            _productRepository = productRepository;
        }

        public async Task<ErrorOr<GetPlayerProfileResponse>> Handle(
            GetPlayerProfileQuery request,
            CancellationToken cancellationToken)
        {
            var targetUser = await _userRepository.GetByIdAsync(
                request.TargetPlayerId,
                cancellationToken);

            if (targetUser == null)
                return Error.NotFound("Player.NotFound", "Player not found.");

            string? borderAssetKey = null;
            if (targetUser.ActiveAvatarBorderId.HasValue)
            {
                var borderProduct = await _productRepository.GetByIdAsync(
                    targetUser.ActiveAvatarBorderId.Value,
                    cancellationToken);

                borderAssetKey = borderProduct?.AssetKey;
            }

            return new GetPlayerProfileResponse(
                PlayerId: targetUser.Id,
                Name: targetUser.FullName,
                Avatar: targetUser.AvatarRef.ToString(),
                Border: borderAssetKey,
                Level: targetUser.Level,
                Coins: targetUser.Coin,
                IsVip: targetUser.IsVip,
                WonHands: targetUser.Wins,
                LostHands: targetUser.Loses
            );
        }
    }
}