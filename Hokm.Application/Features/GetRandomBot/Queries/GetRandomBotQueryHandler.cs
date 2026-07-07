using Hokm.Application.DTOs;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.GetRandomBot.Queries
{
    public class GetRandomBotQueryHandler : IRequestHandler<GetRandomBotQuery, List<PlayerDto>>
    {
        private readonly IUserRepository _userRepository;

        public GetRandomBotQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<List<PlayerDto>> Handle(GetRandomBotQuery request, CancellationToken cancellationToken)
        {
            var bots = await _userRepository.GetRandomBotsAsync(request.Count, request.ExcludeUserIds, cancellationToken);

            return bots.Select(b => new PlayerDto
            {
                PlayerId = b.Id,
                Name = b.FullName,
                Level = b.Level,
                Avatar = b.AvatarRef
            }).ToList();
        }
    }
}
