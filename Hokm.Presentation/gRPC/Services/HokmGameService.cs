using Grpc.Core;
using Hokm.Application.DTOs;
using Hokm.Application.Features.AutoPlay.Commands.EnableAutoPlay;
using Hokm.Application.Features.AutoPlay.Commands.ResumeControl;
using Hokm.Application.Features.DealCards.Command;
using Hokm.Application.Features.FormTeam.Commands;
using Hokm.Application.Features.GameStarted.Commands;
using Hokm.Application.Features.GameStarted.Queries;
using Hokm.Application.Features.PickTrump.Commands;
using Hokm.Application.Features.PlayCard.Commands;
using Hokm.Application.Features.Snapshot.Queries;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using MediatR;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Hokm.Presentation.gRPC.Services
{
    public class HokmGameService : Hokm.HokmGameService.HokmGameServiceBase
    {
        private readonly GameExecutionCoordinator _coordinator;
        private readonly GameStreamingService _streamingService;
        private readonly IMediator _mediator;
        private static readonly object _matchmakingLock = new object();

        public class MatchmakingSession
        {
            public Guid SessionId { get; set; } = Guid.NewGuid();
            public TableKind TableKind { get; set; }
            public int Rounds { get; set; }
            public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();
            public object Lock { get; } = new object();
        }

        private static readonly ConcurrentDictionary<Guid, MatchmakingSession> ActiveLobbies =
            new ConcurrentDictionary<Guid, MatchmakingSession>();

        public static readonly ConcurrentDictionary<Guid, Guid> PlayerActiveGames =
            new ConcurrentDictionary<Guid, Guid>();

        public HokmGameService(GameExecutionCoordinator coordinator, GameStreamingService streamingService, IMediator mediator)
        {
            _coordinator = coordinator;
            _streamingService = streamingService;
            _mediator = mediator;
        }

        // HokmGameService.cs -> StartGame Method
        public override async Task<StartGameResponse> StartGame(StartGameRequest request, ServerCallContext context)
        {
            Guid playerId;
            string playerName;

            var httpContext = context.GetHttpContext();
            var userIdStr = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(userIdStr, out var claimId))
            {
                playerId = claimId;
                playerName = request.PlayerName
                             ?? httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                             ?? "Player";
            }
            else if (!string.IsNullOrEmpty(request.PlayerId) && Guid.TryParse(request.PlayerId, out var reqId))
            {
                playerId = reqId;
                playerName = request.PlayerName ?? "Player";
            }
            else
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه کاربر نامعتبر است."));
            }

            MatchmakingSession targetSession = null;

            lock (_matchmakingLock)
            {
                foreach (var lobby in ActiveLobbies.Values)
                {
                    lock (lobby.Lock)
                    {
                        if (lobby.TableKind == request.TableKind &&
                            lobby.Rounds == request.Rounds &&
                            lobby.Players.Count < 4 &&
                            !lobby.Players.Any(p => p.PlayerId == playerId))
                        {
                            targetSession = lobby;
                            break;
                        }
                    }
                }

                if (targetSession == null)
                {
                    targetSession = new MatchmakingSession
                    {
                        TableKind = request.TableKind,
                        Rounds = request.Rounds
                    };
                    ActiveLobbies.TryAdd(targetSession.SessionId, targetSession);
                }

                lock (targetSession.Lock)
                {
                    if (!targetSession.Players.Any(p => p.PlayerId == playerId))
                    {
                        var assignedSide = targetSession.Players.Count switch
                        {
                            0 => PlayerSide.South,
                            1 => PlayerSide.West,
                            2 => PlayerSide.North,
                            _ => PlayerSide.East
                        };

                        // استعلام لول و آواتار واقعی بازیکن
                        int avatarId = 1;
                        int userLevel = 1;

                        try
                        {
                            var profileQuery = new Hokm.Application.Features.profile.Queries.GetProfile.GetProfileQuery(playerId);
                            var profileResult = _mediator.Send(profileQuery, context.CancellationToken).GetAwaiter().GetResult();

                            profileResult.Match(
                                success => {
                                    avatarId = success.AvatarRef;
                                    userLevel = success.Level;
                                    return true;
                                },
                                errors => false
                            );
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading user profile for lobby: {ex.Message}");
                        }

                        targetSession.Players.Add(new PlayerDto
                        {
                            PlayerId = playerId,
                            Name = playerName,
                            Side = assignedSide,
                            Level = userLevel,
                            Avatar = avatarId
                        });
                    }
                }
            }

            bool triggerLaunch = false;
            Guid actualGameId = Guid.Empty;

            lock (targetSession.Lock)
            {
                if (targetSession.Players.Count == 4)
                {
                    triggerLaunch = true;
                    ActiveLobbies.TryRemove(targetSession.SessionId, out _);
                }
            }

            if (triggerLaunch)
            {
                var startCmd = new StartGameCommand
                {
                    Player1 = targetSession.Players[0],
                    Player2 = targetSession.Players[1],
                    Player3 = targetSession.Players[2],
                    Player4 = targetSession.Players[3]
                };
                actualGameId = await _mediator.Send(startCmd);

                foreach (var player in targetSession.Players)
                {
                    PlayerActiveGames[player.PlayerId] = actualGameId;
                }

                var formTeamCmd = new FormTeamCommand { GameId = actualGameId };
                await _coordinator.ExecuteAsync(actualGameId, formTeamCmd, context.CancellationToken);

                var dealer = targetSession.Players.First(p => p.Side == PlayerSide.South);
                var dealCmd = new DealCardsCommand
                {
                    DealerId = dealer.PlayerId,
                    GameId = actualGameId
                };
                await _coordinator.ExecuteAsync(actualGameId, dealCmd, context.CancellationToken);

                var gameStartedEvent = new GameEvent
                {
                    EventType = "game_ready",
                    Payload = JsonSerializer.Serialize(new { GameId = actualGameId.ToString() })
                };
                await _streamingService.BroadcastAsync(targetSession.SessionId, gameStartedEvent, context.CancellationToken);

                return new StartGameResponse { GameId = actualGameId.ToString() };
            }
            else
            {
                var updateEvent = new GameEvent
                {
                    EventType = "player_joined",
                    Payload = JsonSerializer.Serialize(new
                    {
                        ConnectedCount = targetSession.Players.Count,
                        Players = targetSession.Players.Select(p => new
                        {
                            PlayerId = p.PlayerId.ToString(),
                            Name = p.Name,
                            Side = p.Side.ToString(),
                            Avatar = p.Avatar, // فرستادن اطلاعات کامل
                            Level = p.Level    // فرستادن اطلاعات کامل
                        }).ToList()
                    })
                };
                await _streamingService.BroadcastAsync(targetSession.SessionId, updateEvent, context.CancellationToken);
            }

            return new StartGameResponse { GameId = targetSession.SessionId.ToString() };
        }

        public override async Task<LeaveLobbyResponse> LeaveLobby(LeaveLobbyRequest request, ServerCallContext context)
        {
            if (Guid.TryParse(request.LobbyId, out var lobbyId) && Guid.TryParse(request.PlayerId, out var playerId))
            {
                if (ActiveLobbies.TryGetValue(lobbyId, out var lobby))
                {
                    lock (lobby.Lock)
                    {
                        lobby.Players.RemoveAll(p => p.PlayerId == playerId);
                        if (lobby.Players.Count == 0)
                        {
                            ActiveLobbies.TryRemove(lobbyId, out _);
                            return new LeaveLobbyResponse { Success = true };
                        }
                    }

                    var updateEvent = new GameEvent
                    {
                        EventType = "player_joined",
                        Payload = JsonSerializer.Serialize(new
                        {
                            ConnectedCount = lobby.Players.Count,
                            Players = lobby.Players.Select(p => new
                            {
                                PlayerId = p.PlayerId.ToString(),
                                Name = p.Name,
                                Side = p.Side.ToString(),
                                Avatar = p.Avatar,
                                Level = p.Level
                            }).ToList()
                        })
                    };
                    await _streamingService.BroadcastAsync(lobbyId, updateEvent, context.CancellationToken);

                    return new LeaveLobbyResponse { Success = true };
                }
            }
            return new LeaveLobbyResponse { Success = false };
        }

        public override async Task StreamGame(StreamRequest request, IServerStreamWriter<GameEvent> responseStream, ServerCallContext context)
        {
            var gameId = Guid.Parse(request.GameId);
            var playerId = Guid.Parse(request.PlayerId);
            var subscription = _streamingService.Subscribe(gameId, playerId);

            if (ActiveLobbies.TryGetValue(gameId, out var lobby))
            {
                int count;
                List<object> playersList = null;

                lock (lobby.Lock)
                {
                    count = lobby.Players.Count;
                    playersList = lobby.Players.Select(p => new
                    {
                        PlayerId = p.PlayerId.ToString(),
                        Name = p.Name,
                        Side = p.Side.ToString(),
                        Level = p.Level,
                        Avatar = p.Avatar
                    }).Cast<object>().ToList();
                }

                await responseStream.WriteAsync(new GameEvent
                {
                    EventType = "player_joined",
                    Payload = JsonSerializer.Serialize(new
                    {
                        ConnectedCount = count,
                        Players = playersList
                    })
                }, context.CancellationToken);
            }

            if (!ActiveLobbies.ContainsKey(gameId))
            {
                try
                {
                    // M2: بازیکن به بازی فعال متصل شد. وضعیت او را به صورت آنلاین (True) برای بقیه برادکست می‌کنیم
                    var statusEvent = new GameEvent
                    {
                        EventType = "player_status_changed",
                        Payload = JsonSerializer.Serialize(new
                        {
                            PlayerId = playerId.ToString(),
                            IsOnline = true
                        })
                    };
                    await _streamingService.BroadcastAsync(gameId, statusEvent, context.CancellationToken);

                    var snapshot = await _mediator.Send(new GetGameSnapshotQuery
                    {
                        GameId = gameId,
                        PlayerId = playerId
                    }, context.CancellationToken);

                    if (snapshot != null)
                    {
                        var hostPlayer = snapshot.Players.FirstOrDefault(p => p.Side == "South");

                        if (hostPlayer != null)
                        {
                            var hostPlayerId = hostPlayer.PlayerId;

                            if (playerId == hostPlayerId)
                            {
                                if (snapshot.Status == "WaitingForTeams")
                                {
                                    var formTeamCmd = new FormTeamCommand { GameId = gameId };
                                    await _coordinator.ExecuteAsync(gameId, formTeamCmd, context.CancellationToken);

                                    snapshot.Status = "TeamsReady";
                                }

                                if (snapshot.Status == "TeamsReady")
                                {
                                    var dealCmd = new DealCardsCommand
                                    {
                                        DealerId = hostPlayerId,
                                        GameId = gameId
                                    };
                                    await _coordinator.ExecuteAsync(gameId, dealCmd, context.CancellationToken);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error starting game auto-commands: {ex.Message}");
                }
            }

            try
            {
                await foreach (var gameEvent in subscription.EventChannel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    await responseStream.WriteAsync(gameEvent, context.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                await HandleGameDisconnect(gameId, playerId);
            }
            finally
            {
                _streamingService.Unsubscribe(subscription);
                await HandleGameDisconnect(gameId, playerId);
            }
        }

        // متد اختصاصی استریم لابی انتظار در HokmGameService.cs
        public override async Task StreamLobby(StreamRequest request, IServerStreamWriter<GameEvent> responseStream, ServerCallContext context)
        {
            var lobbyId = Guid.Parse(request.GameId);
            var playerId = Guid.Parse(request.PlayerId);

            // سابسکرایب به کانال لابی
            var subscription = _streamingService.Subscribe(lobbyId, playerId);

            if (ActiveLobbies.TryGetValue(lobbyId, out var lobby))
            {
                int count;
                List<object> playersList = null;

                lock (lobby.Lock)
                {
                    count = lobby.Players.Count;
                    playersList = lobby.Players.Select(p => new
                    {
                        PlayerId = p.PlayerId.ToString(),
                        Name = p.Name,
                        Side = p.Side.ToString(),
                        Level = p.Level,
                        Avatar = p.Avatar
                    }).Cast<object>().ToList();
                }

                await responseStream.WriteAsync(new GameEvent
                {
                    EventType = "player_joined",
                    Payload = JsonSerializer.Serialize(new
                    {
                        ConnectedCount = count,
                        Players = playersList
                    })
                }, context.CancellationToken);
            }

            try
            {
                await foreach (var gameEvent in subscription.EventChannel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    await responseStream.WriteAsync(gameEvent, context.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                await HandleLobbyDisconnect(lobbyId, playerId);
            }
            finally
            {
                _streamingService.Unsubscribe(subscription);
                await HandleLobbyDisconnect(lobbyId, playerId);
            }
        }
        // ۱. متد اختصاصی دیسکانکت لابی انتظار (تمیز و بدون نشت)
        private async Task HandleLobbyDisconnect(Guid lobbyId, Guid playerId)
        {
            Console.WriteLine($"=== [Lobby Disconnect] Player: {playerId} left Lobby: {lobbyId} ===");

            if (ActiveLobbies.TryGetValue(lobbyId, out var lobby))
            {
                bool isEmpty = false;
                lock (lobby.Lock)
                {
                    lobby.Players.RemoveAll(p => p.PlayerId == playerId);
                    if (lobby.Players.Count == 0)
                    {
                        isEmpty = true;
                    }
                }

                if (isEmpty)
                {
                    ActiveLobbies.TryRemove(lobbyId, out _);
                }
                else
                {
                    var updateEvent = new GameEvent
                    {
                        EventType = "player_joined",
                        Payload = JsonSerializer.Serialize(new
                        {
                            ConnectedCount = lobby.Players.Count,
                            Players = lobby.Players.Select(p => new
                            {
                                PlayerId = p.PlayerId.ToString(),
                                Name = p.Name,
                                Side = p.Side.ToString(),
                                Avatar = p.Avatar,
                                Level = p.Level
                            }).ToList()
                        })
                    };
                    await _streamingService.BroadcastAsync(lobbyId, updateEvent, CancellationToken.None);
                }
            }
        }

        private async Task HandleGameDisconnect(Guid gameId, Guid playerId)
        {
            var statusEvent = new GameEvent
            {
                EventType = "player_status_changed",
                Payload = JsonSerializer.Serialize(new
                {
                    PlayerId = playerId.ToString(),
                    IsOnline = false
                })
            };
            await _streamingService.BroadcastAsync(gameId, statusEvent, CancellationToken.None);

            var enableAutoPlayCmd = new EnableAutoPlayCommand(gameId, playerId);
            await _coordinator.ExecuteAsync(gameId, enableAutoPlayCmd, CancellationToken.None);
        }

        public override async Task<ResumeControlResponse> ResumeControl(ResumeControlRequest request, ServerCallContext context)
        {
            var cmd = new ResumeControlCommand(
                Guid.Parse(request.GameId),
                Guid.Parse(request.PlayerId)
            );

            await _coordinator.ExecuteAsync(cmd.GameId, cmd, context.CancellationToken);

            return new ResumeControlResponse { Success = true };
        }

        public override async Task<InGameActionResponse> SendInGameAction(InGameActionRequest request, ServerCallContext context)
        {
            var gameId = Guid.Parse(request.GameId);

            var actionEvent = new GameEvent
            {
                EventType = "ingame_action",
                Payload = JsonSerializer.Serialize(new
                {
                    PlayerId = request.PlayerId,
                    ActionType = request.ActionType,
                    Content = request.Content
                })
            };

            await _streamingService.BroadcastAsync(gameId, actionEvent, context.CancellationToken);

            return new InGameActionResponse { Success = true };
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
    }
}