using Hokm.Domain.Entities;
using Hokm.Domain.Enums;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.GameStarted.Commands
{
    public class StartGameCommandHandler : IRequestHandler<StartGameCommand, Guid>
    {
        private readonly IGameRepository _gameRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProductRepository _productRepository;

        public StartGameCommandHandler(
            IGameRepository gameRepository,
            IUserRepository userRepository,
            IProductRepository productRepository)
        {
            _gameRepository = gameRepository;
            _userRepository = userRepository;
            _productRepository = productRepository;
        }

        public async Task<Guid> Handle(StartGameCommand request, CancellationToken cancellationToken)
        {
            var player1 = new Player(request.Player1.PlayerId, request.Player1.Name, PlayerSide.South);
            var player2 = new Player(request.Player2.PlayerId, request.Player2.Name, PlayerSide.West);
            var player3 = new Player(request.Player3.PlayerId, request.Player3.Name, PlayerSide.North);
            var player4 = new Player(request.Player4.PlayerId, request.Player4.Name, PlayerSide.East);

            await SetPlayerAvatarAndThemesAsync(player1, cancellationToken);
            await SetPlayerAvatarAndThemesAsync(player2, cancellationToken);
            await SetPlayerAvatarAndThemesAsync(player3, cancellationToken);
            await SetPlayerAvatarAndThemesAsync(player4, cancellationToken);

            var newGame = new Domain.Entities.Game(player1, player2, player3, player4, request.TableKind);
            await _gameRepository.SaveAsync(newGame, cancellationToken);

            return newGame.Id;
        }

        private async Task SetPlayerAvatarAndThemesAsync(Player player, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(player.Id, cancellationToken);
            if (user != null)
            {
                player.UserId = user.Id;
                player.SetAvatarRef(user.AvatarRef);

                if (user.IsBot)
                {
                    player.EnableAutoPlay();
                }

                if (user.ActiveCardThemeId.HasValue && user.ActiveCardThemeId.Value != Guid.Empty)
                {
                    var cardProduct = await _productRepository.GetByIdAsync(user.ActiveCardThemeId.Value, cancellationToken);
                    if (cardProduct != null && !string.IsNullOrWhiteSpace(cardProduct.AssetKey))
                    {
                        player.SetCardSkin(cardProduct.AssetKey);
                    }
                }

                if (user.ActiveTableThemeId.HasValue && user.ActiveTableThemeId.Value != Guid.Empty)
                {
                    var tableProduct = await _productRepository.GetByIdAsync(user.ActiveTableThemeId.Value, cancellationToken);
                    if (tableProduct != null && !string.IsNullOrWhiteSpace(tableProduct.AssetKey))
                    {
                        player.SetBoardTheme(tableProduct.AssetKey);
                    }
                }
            }
        }
    }
}