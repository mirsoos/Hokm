using ErrorOr;
using Hokm.Domain.Enums;
using MediatR;

namespace Hokm.Application.Features.TableConfig.Queries.GetTableConfigs
{
    public record GetTableConfigsQuery : IRequest<ErrorOr<List<TableConfigDto>>>;

    public record TableConfigDto(
        TableKind Kind,
        int EntryFee,
        int Prize,
        int Rounds,
        int WinXp,
        int LossXp
    );
}
