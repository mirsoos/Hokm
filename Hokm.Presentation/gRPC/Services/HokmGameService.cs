using Grpc.Core;
using Hokm.Application.Constants;
using Hokm.Application.DTOs;
using Hokm.Application.Features.AutoPlay.Commands.EnableAutoPlay;
using Hokm.Application.Features.AutoPlay.Commands.ResumeControl;
using Hokm.Application.Features.DealCards.Command;
using Hokm.Application.Features.DeductCoins.Commands;
using Hokm.Application.Features.FormTeam.Commands;
using Hokm.Application.Features.GameStarted.Commands;
using Hokm.Application.Features.GameStarted.Queries;
using Hokm.Application.Features.GetRandomBot.Queries;
using Hokm.Application.Features.PickTrump.Commands;
using Hokm.Application.Features.PlayCard.Commands;
using Hokm.Application.Features.Snapshot.Queries;
using Hokm.Application.Features.StartPlayingPhase;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using MediatR;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Hokm.Presentation.gRPC.Services
{
    public class HokmGameService : Hokm.HokmGameService.HokmGameServiceBase
    {
        private readonly GameExecutionCoordinator _coordinator;
        private readonly GameStreamingService _streamingService;
        private readonly IMediator _mediator;
        private readonly IServiceScopeFactory _scopeFactory;
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

        public HokmGameService(GameExecutionCoordinator coordinator, GameStreamingService streamingService, IMediator mediator,IServiceScopeFactory scopeFactory)
        {
            _coordinator = coordinator;
            _streamingService = streamingService;
            _mediator = mediator;
            _scopeFactory = scopeFactory;
        }


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

            int avatarId = 1;
            int userLevel = 1;
            var requiredFee = GameConstants.GetTableFee((Domain.Enums.TableKind)request.TableKind);

            try
            {
                var profileQuery = new Application.Features.profile.Queries.GetProfile.GetProfileQuery(playerId);
                var profileResult = await _mediator.Send(profileQuery, context.CancellationToken);

                bool coinCheckPassed = false;
                string errorMessage = "";

                profileResult.Match(
                    success => {
                        avatarId = success.AvatarRef;
                        userLevel = success.Level;

                        if (success.Coin < requiredFee)
                        {
                            errorMessage = "سکه شما برای ورود به این میز کافی نیست.";
                        }
                        else
                        {
                            coinCheckPassed = true;
                        }
                        return true;
                    },
                    errors => {
                        errorMessage = "خطا در استعلام پروفایل کاربر.";
                        return false;
                    }
                );

                if (!coinCheckPassed)
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, errorMessage));
                }

            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"خطای غیرمنتظره سرور: {ex.Message}"));
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

                    _ = StartGhostBotFiller(targetSession.SessionId);
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
                Guid actualGameId = await LaunchGame(targetSession);
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
                            Avatar = p.Avatar,
                            Level = p.Level
                        }).ToList()
                    })
                };
                await _streamingService.BroadcastAsync(targetSession.SessionId, updateEvent, context.CancellationToken);
            }

            return new StartGameResponse { GameId = targetSession.SessionId.ToString() };
        }

        private async Task StartGhostBotFiller(Guid sessionId)
        {
            await Task.Delay(TimeSpan.FromSeconds(5.0));

            while (ActiveLobbies.TryGetValue(sessionId, out var lobby))
            {
                if (lobby.Players.Count >= 4)
                    break;

                var randomDelay = Random.Shared.Next(2000, 5000);
                await Task.Delay(randomDelay);

                if (!ActiveLobbies.TryGetValue(sessionId, out lobby) || lobby.Players.Count >= 4)
                    break;

                var currentLobbyPlayerIds = lobby.Players.Select(p => p.PlayerId).ToList();

                var query = new GetRandomBotQuery(1, currentLobbyPlayerIds);

                List<PlayerDto> dbBots;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    dbBots = await scopedMediator.Send(query, CancellationToken.None);
                }

                if (dbBots == null || !dbBots.Any())
                    break;

                var botDto = dbBots.First();

                lock (lobby.Lock)
                {
                    if (lobby.Players.Count >= 4) break;

                    var assignedSide = lobby.Players.Count switch
                    {
                        0 => PlayerSide.South,
                        1 => PlayerSide.West,
                        2 => PlayerSide.North,
                        _ => PlayerSide.East
                    };

                    botDto.Side = assignedSide;
                    lobby.Players.Add(botDto);
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
                            p.Name,
                            Side = p.Side.ToString(),
                            p.Avatar,
                            p.Level
                        }).ToList()
                    })
                };
                await _streamingService.BroadcastAsync(sessionId, updateEvent, CancellationToken.None);

                if (lobby.Players.Count == 4)
                {
                    if (ActiveLobbies.TryRemove(sessionId, out _))
                    {
                        await LaunchGame(lobby);
                    }
                    break;
                }
            }
        }

        private async Task<Guid> LaunchGame(MatchmakingSession session)
        {
            // ساخت اسکوپ مستقل در شروع اجرای متد جهت تامین ایمن Mediator در فرآیندهای پس‌زمینه ربات‌ها
            using (var scope = _scopeFactory.CreateScope())
            {
                var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var startCmd = new StartGameCommand
                {
                    Player1 = session.Players[0],
                    Player2 = session.Players[1],
                    Player3 = session.Players[2],
                    Player4 = session.Players[3],
                    TableKind = (Domain.Enums.TableKind)session.TableKind
                };

                List<Guid> players = session.Players
                      .Select(p => p.PlayerId)
                      .ToList();

                var requiredFee = GameConstants.GetTableFee((Domain.Enums.TableKind)session.TableKind);

                if (requiredFee > 0)
                {
                    var deductCoinsCmd = new DeductCoinsCommand(players, requiredFee);

                    try
                    {
                        // استفاده از اسکوپ فعال و زنده برای تراکنش مالی
                        await scopedMediator.Send(deductCoinsCmd, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        var errorEvent = new GameEvent
                        {
                            EventType = "game_launch_failed",
                            Payload = JsonSerializer.Serialize(new
                            {
                                Reason = "insufficient_coins",
                                Message = "شروع بازی به دلیل عدم موجودی یا خطای تراکنش مالی یکی از بازیکنان لغو شد."
                            })
                        };
                        await _streamingService.BroadcastAsync(session.SessionId, errorEvent, CancellationToken.None);

                        throw new RpcException(new Status(StatusCode.FailedPrecondition, "خطای تراکنش مالی گروهی در شروع بازی."));
                    }
                }

                // استفاده از اسکوپ فعال برای استارت بازی
                var actualGameId = await scopedMediator.Send(startCmd);

                foreach (var player in session.Players)
                {
                    PlayerActiveGames[player.PlayerId] = actualGameId;
                }

                var formTeamCmd = new FormTeamCommand { GameId = actualGameId };
                await _coordinator.ExecuteAsync(actualGameId, formTeamCmd, CancellationToken.None);

                var dealer = session.Players.First(p => p.Side == PlayerSide.South);
                var dealCmd = new DealCardsCommand
                {
                    DealerId = dealer.PlayerId,
                    GameId = actualGameId
                };
                await _coordinator.ExecuteAsync(actualGameId, dealCmd, CancellationToken.None);

                var gameStartedEvent = new GameEvent
                {
                    EventType = "game_ready",
                    Payload = JsonSerializer.Serialize(new { GameId = actualGameId.ToString() })
                };
                await _streamingService.BroadcastAsync(session.SessionId, gameStartedEvent, CancellationToken.None);

                return actualGameId;
            }
        }
        //public override async Task<StartGameResponse> StartGame(StartGameRequest request, ServerCallContext context)
        //{
        //    Guid playerId;
        //    string playerName;

        //    var httpContext = context.GetHttpContext();
        //    var userIdStr = httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        //    if (Guid.TryParse(userIdStr, out var claimId))
        //    {
        //        playerId = claimId;
        //        playerName = request.PlayerName
        //                     ?? httpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        //                     ?? "Player";
        //    }
        //    else if (!string.IsNullOrEmpty(request.PlayerId) && Guid.TryParse(request.PlayerId, out var reqId))
        //    {
        //        playerId = reqId;
        //        playerName = request.PlayerName ?? "Player";
        //    }
        //    else
        //    {
        //        throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه کاربر نامعتبر است."));
        //    }

        //    int avatarId = 1;
        //    int userLevel = 1;
        //    var requiredFee = GameConstants.GetTableFee((Hokm.Domain.Enums.TableKind)request.TableKind);

        //    try
        //    {
        //        var profileQuery = new Application.Features.profile.Queries.GetProfile.GetProfileQuery(playerId);
        //        var profileResult = await _mediator.Send(profileQuery, context.CancellationToken);

        //        bool coinCheckPassed = false;
        //        string errorMessage = "";

        //        profileResult.Match(
        //            success => {
        //                avatarId = success.AvatarRef;
        //                userLevel = success.Level;

        //                if (success.Coin < requiredFee)
        //                {
        //                    errorMessage = "سکه شما برای ورود به این میز کافی نیست.";
        //                }
        //                else
        //                {
        //                    coinCheckPassed = true;
        //                }
        //                return true;
        //            },
        //            errors => {
        //                errorMessage = "خطا در استعلام پروفایل کاربر.";
        //                return false;
        //            }
        //        );

        //        if (!coinCheckPassed)
        //        {
        //            throw new RpcException(new Status(StatusCode.FailedPrecondition, errorMessage));
        //        }

        //        if (requiredFee > 0)
        //        {
        //            var deductCoinsCmd = new DeductCoinsCommand(playerId, requiredFee);
        //            await _mediator.Send(deductCoinsCmd, context.CancellationToken);
        //        }
        //    }
        //    catch (RpcException)
        //    {
        //        throw;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new RpcException(new Status(StatusCode.Internal, $"خطای غیرمنتظره سرور: {ex.Message}"));
        //    }

        //    MatchmakingSession targetSession = null;

        //    lock (_matchmakingLock)
        //    {
        //        foreach (var lobby in ActiveLobbies.Values)
        //        {
        //            lock (lobby.Lock)
        //            {
        //                if (lobby.TableKind == request.TableKind &&
        //                    lobby.Rounds == request.Rounds &&
        //                    lobby.Players.Count < 4 &&
        //                    !lobby.Players.Any(p => p.PlayerId == playerId))
        //                {
        //                    targetSession = lobby;
        //                    break;
        //                }
        //            }
        //        }

        //        if (targetSession == null)
        //        {
        //            targetSession = new MatchmakingSession
        //            {
        //                TableKind = request.TableKind,
        //                Rounds = request.Rounds
        //            };
        //            ActiveLobbies.TryAdd(targetSession.SessionId, targetSession);
        //        }

        //        lock (targetSession.Lock)
        //        {
        //            if (!targetSession.Players.Any(p => p.PlayerId == playerId))
        //            {
        //                var assignedSide = targetSession.Players.Count switch
        //                {
        //                    0 => PlayerSide.South,
        //                    1 => PlayerSide.West,
        //                    2 => PlayerSide.North,
        //                    _ => PlayerSide.East
        //                };

        //                targetSession.Players.Add(new PlayerDto
        //                {
        //                    PlayerId = playerId,
        //                    Name = playerName,
        //                    Side = assignedSide,
        //                    Level = userLevel,
        //                    Avatar = avatarId
        //                });
        //            }
        //        }
        //    }

        //    bool triggerLaunch = false;
        //    Guid actualGameId = Guid.Empty;

        //    lock (targetSession.Lock)
        //    {
        //        if (targetSession.Players.Count == 4)
        //        {
        //            triggerLaunch = true;
        //            ActiveLobbies.TryRemove(targetSession.SessionId, out _);
        //        }
        //    }

        //    if (triggerLaunch)
        //    {
        //        var startCmd = new StartGameCommand
        //        {
        //            Player1 = targetSession.Players[0],
        //            Player2 = targetSession.Players[1],
        //            Player3 = targetSession.Players[2],
        //            Player4 = targetSession.Players[3],
        //            TableKind = (Domain.Enums.TableKind)targetSession.TableKind
        //        };
        //        actualGameId = await _mediator.Send(startCmd);

        //        foreach (var player in targetSession.Players)
        //        {
        //            PlayerActiveGames[player.PlayerId] = actualGameId;
        //        }

        //        var formTeamCmd = new FormTeamCommand { GameId = actualGameId };
        //        await _coordinator.ExecuteAsync(actualGameId, formTeamCmd, context.CancellationToken);

        //        var dealer = targetSession.Players.First(p => p.Side == PlayerSide.South);
        //        var dealCmd = new DealCardsCommand
        //        {
        //            DealerId = dealer.PlayerId,
        //            GameId = actualGameId
        //        };
        //        await _coordinator.ExecuteAsync(actualGameId, dealCmd, context.CancellationToken);

        //        var gameStartedEvent = new GameEvent
        //        {
        //            EventType = "game_ready",
        //            Payload = JsonSerializer.Serialize(new { GameId = actualGameId.ToString() })
        //        };
        //        await _streamingService.BroadcastAsync(targetSession.SessionId, gameStartedEvent, context.CancellationToken);

        //        return new StartGameResponse { GameId = actualGameId.ToString() };
        //    }
        //    else
        //    {
        //        var updateEvent = new GameEvent
        //        {
        //            EventType = "player_joined",
        //            Payload = JsonSerializer.Serialize(new
        //            {
        //                ConnectedCount = targetSession.Players.Count,
        //                Players = targetSession.Players.Select(p => new
        //                {
        //                    PlayerId = p.PlayerId.ToString(),
        //                    Name = p.Name,
        //                    Side = p.Side.ToString(),
        //                    Avatar = p.Avatar,
        //                    Level = p.Level
        //                }).ToList()
        //            })
        //        };
        //        await _streamingService.BroadcastAsync(targetSession.SessionId, updateEvent, context.CancellationToken);
        //    }

        //    return new StartGameResponse { GameId = targetSession.SessionId.ToString() };
        //}
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
                Payload = JsonSerializer.Serialize(new { PlayerId = playerId.ToString(), IsOnline = false, IsAutoPlay = true })
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

        public override async Task<ReadyToPlayResponse> ReadyToPlay(ReadyToPlayRequest request, ServerCallContext context)
        {
            var gameId = Guid.Parse(request.GameId);

            var cmd = new StartPlayingPhaseCommand(gameId);
            await _coordinator.ExecuteAsync(gameId, cmd, context.CancellationToken);

            return new ReadyToPlayResponse { Success = true };
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