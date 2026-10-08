using ErrorOr;
using Hokm.Application.Constants;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.TableConfig.Queries.GetTableConfigs
{
    public class GetTableConfigsQueryHandler
            : IRequestHandler<GetTableConfigsQuery, ErrorOr<List<TableConfigDto>>>
    {
        public Task<ErrorOr<List<TableConfigDto>>> Handle(
            GetTableConfigsQuery request,
            CancellationToken cancellationToken)
        {
            var kinds = new[]
            {
                TableKind.Bot,
                TableKind.Speedy,
                TableKind.Pro,
                TableKind.Vip,
            };

            var result = kinds.Select(k => new TableConfigDto(
                Kind: k,
                EntryFee: GameConstants.GetTableFee(k),
                Prize: GameConstants.GetTablePrize(k),
                Rounds: GameConstants.GetTargetRounds(k),
                WinXp: GameConstants.GetTableWinXp(k),
                LossXp: GameConstants.GetTableLossXp(k)
            )).ToList();

            return Task.FromResult<ErrorOr<List<TableConfigDto>>>(result);
        }
    }
}
