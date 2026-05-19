using Grpc.Core;
using Hokm.Application.DTOs;
using Hokm.Application.Features.DealCards.Command;
using Hokm.Application.Features.FormTeam.Commands;
using Hokm.Application.Features.GameStarted.Commands;
using Hokm.Application.Features.GameStarted.Queries;
using Hokm.Application.Features.PickTrump.Commands;
using Hokm.Application.Features.PlayCard.Commands;
using Hokm.Application.Features.Snapshot.Queries;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using Hokm.GrpcService.Realtime;
using MediatR;
using System.Text.Json;

namespace Hokm.GrpcService.Services
{
    public class HokmGameService : Hokm.HokmGameService.HokmGameServiceBase
    {
        private readonly GameExecutionCoordinator _coordinator;
        private readonly GameStreamingService _streamingService;
        private readonly IMediator _mediator;
        public HokmGameService(GameExecutionCoordinator coordinator, GameStreamingService streamingService, IMediator mediator)
        {
            _coordinator = coordinator;
            _streamingService = streamingService;
            _mediator = mediator;
        }

        public override async Task<StartGameResponse> StartGame(StartGameRequest request, ServerCallContext context)
        {
            var cmd = new StartGameCommand
            {
                Player1 = ToNewPlayer(request.Player1Id,request.Player1Name,request.Player1Side),
                Player2 = ToNewPlayer(request.Player2Id,request.Player2Name,request.Player2Side),
                Player3 = ToNewPlayer(request.Player3Id,request.Player3Name,request.Player3Side),
                Player4 = ToNewPlayer(request.Player4Id,request.Player4Name,request.Player4Side),
            };
            var gameId = await _mediator.Send(cmd);
            return new StartGameResponse { GameId = gameId.ToString() };
        }
        public override async Task<FormTeamsResponse> FormTeams(FormTeamsRequest request, ServerCallContext context)
        {
            var cmd = new FormTeamCommand
            {
                GameId = Guid.Parse(request.GameId)
            };
            var result = await _coordinator.ExecuteAsync(
                cmd.GameId,
                cmd,
                context.CancellationToken);
            return new FormTeamsResponse { GameId = result.GameId.ToString() };
        }
        public override async Task<DealCardsResponse> DealCards(DealCardsRequest request, ServerCallContext context)
        {
            var cmd = new DealCardsCommand
            {
                DealerId = Guid.Parse(request.DealerId),
                GameId = Guid.Parse(request.GameId)
            };
            await _coordinator.ExecuteAsync(
                cmd.GameId,
                cmd,
                context.CancellationToken);
            return new DealCardsResponse { Success = true };
        }
        public override async Task<PickTrumpResponse> PickTrump(PickTrumpRequest request, ServerCallContext context)
        {
            var cmd = new PickTrumpCommand
            {
                DealerId = Guid.Parse(request.DealerId),
                GameId = Guid.Parse(request.GameId),
                TrumpSuit = Enum.Parse<Suit>(request.TrumpSuit)
            };
            await _coordinator.ExecuteAsync(
                cmd.GameId,
                cmd,
                context.CancellationToken);
            return new PickTrumpResponse { Success = true };
        }
        public override async Task<PlayCardResponse> PlayCard(PlayCardRequest request, ServerCallContext context)
        {
            var cmd = new PlayCardCommand
            {
                GameId = Guid.Parse(request.GameId),
                PlayerId = Guid.Parse(request.PlayerId),
                Rank = Enum.Parse<Rank>(request.Rank),
                Suit = Enum.Parse<Suit>(request.Suit)
            };
            await _coordinator.ExecuteAsync(
                cmd.GameId,
                cmd,
                context.CancellationToken);
            return new PlayCardResponse { Success = true };
        }
        public override async Task<GameState> GetGameState(GetGameStateRequest request, ServerCallContext context)
        {
            var cmd = new GetGameStateQuery
            {
                GameId = Guid.Parse(request.GameId)
            };
            var result = await _mediator.Send(cmd);
            return new GameState
            {
                GameId = result.GameId.ToString(),
                Status = result.Status.ToString(),
                CurrentRound = result.CurrentRound
            };
        }
        public override async Task StreamGame(StreamRequest request,IServerStreamWriter<GameEvent> responseStream,ServerCallContext context)
        {
            var gameId = Guid.Parse(request.GameId);
            var playerId = Guid.Parse(request.PlayerId);
            var subscription = _streamingService.Subscribe(gameId,playerId);

            try
            {
                await foreach (var gameEvent in subscription.EventChannel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    await responseStream.WriteAsync(gameEvent,context.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _streamingService.Unsubscribe(subscription);
            }
        }
        public override async Task<GameSnapshotResponse> GetSnapshot(GetSnapshotRequest request, ServerCallContext context)
        {
            var cmd = new GetGameSnapshotQuery
            {
                GameId = Guid.Parse(request.GameId),
                PlayerId = Guid.Parse(request.PlayerId)
            };
            var result = await _mediator.Send(cmd);
            return new GameSnapshotResponse
            {
                Payload = JsonSerializer.Serialize(result)
            };
        }
        private PlayerDto ToNewPlayer(string id, string name, string sideStr)
        {
            return new PlayerDto
            {
                PlayerId = Guid.Parse(id),
                Name = name,
                Side = Enum.Parse<PlayerSide>(sideStr)
            };
        }
    }
}
