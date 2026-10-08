using ErrorOr;
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
using Hokm.Application.Features.GetPlayerProfile.Queries;
using Hokm.Application.Features.GetRandomBot.Queries;
using Hokm.Application.Features.PickTrump.Commands;
using Hokm.Application.Features.PlayCard.Commands;
using Hokm.Application.Features.ReadyToPlay.Commands;
using Hokm.Application.Features.Snapshot.Queries;
using Hokm.Application.Features.TableConfig.Queries.GetTableConfigs;
using Hokm.Application.Features.TrickDetails.Queries.GetTrickDetails;
using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Hokm.Presentation.gRPC.Services
{
    public class HokmGameService : Hokm.HokmGameService.HokmGameServiceBase
    {
        private readonly GameExecutionCoordinator _coordinator;
        private readonly GameStreamingService _streamingService;
        private readonly IMediator _mediator;
        private readonly IServiceScopeFactory _scopeFactory;

        public class MatchmakingSession
        {
            public Guid SessionId { get; set; } = Guid.NewGuid();
            public TableKind TableKind { get; set; }
            public int Rounds { get; set; }
            public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();
            public object Lock { get; } = new object();
            public CancellationTokenSource Cts { get; } = new CancellationTokenSource();
            public bool IsLaunched { get; set; } = false;
        }

        private static readonly ConcurrentDictionary<Guid, MatchmakingSession> ActiveLobbies =
            new ConcurrentDictionary<Guid, MatchmakingSession>();

        public static readonly ConcurrentDictionary<Guid, Guid> PlayerActiveGames =
            new ConcurrentDictionary<Guid, Guid>();

        private static readonly object MatchmakingGlobalLock = new object();

        public HokmGameService(
            GameExecutionCoordinator coordinator,
            GameStreamingService streamingService,
            IMediator mediator,
            IServiceScopeFactory scopeFactory)
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

            if (PlayerActiveGames.TryGetValue(playerId, out var existingGameId))
            {
                return new StartGameResponse { GameId = existingGameId.ToString() };
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
            bool isNewLobbyCreated = false;

            lock (MatchmakingGlobalLock)
            {
                foreach (var lobby in ActiveLobbies.Values)
                {
                    lock (lobby.Lock)
                    {
                        if (lobby.IsLaunched) continue;

                        if (lobby.TableKind == request.TableKind &&
                            lobby.Rounds == request.Rounds &&
                            lobby.Players.Count < 4 &&
                            !lobby.Players.Any(p => p.PlayerId == playerId))
                        {
                            var assignedSide = lobby.Players.Count switch
                            {
                                0 => PlayerSide.South,
                                1 => PlayerSide.West,
                                2 => PlayerSide.North,
                                _ => PlayerSide.East
                            };

                            lobby.Players.Add(new PlayerDto
                            {
                                PlayerId = playerId,
                                Name = playerName,
                                Side = assignedSide,
                                Level = userLevel,
                                Avatar = avatarId,
                            });

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

                    targetSession.Players.Add(new PlayerDto
                    {
                        PlayerId = playerId,
                        Name = playerName,
                        Side = PlayerSide.South,
                        Level = userLevel,
                        Avatar = avatarId,
                    });

                    ActiveLobbies.TryAdd(targetSession.SessionId, targetSession);
                    isNewLobbyCreated = true;
                }
            }

            if (isNewLobbyCreated)
            {
                _ = StartGhostBotFiller(targetSession.SessionId);
            }

            Guid? actualGameId = await CheckAndLaunchGame(targetSession, context.CancellationToken);

            string responseGameId = actualGameId.HasValue
                ? actualGameId.Value.ToString()
                : targetSession.SessionId.ToString();

            return new StartGameResponse { GameId = responseGameId };
        }

        private async Task StartGhostBotFiller(Guid sessionId)
        {
            CancellationTokenSource? cts = null;

            try
            {
                if (!ActiveLobbies.TryGetValue(sessionId, out var lobby))
                    return;

                cts = lobby.Cts;
                var cancellationToken = cts.Token;

                await Task.Delay(TimeSpan.FromSeconds(5.0), cancellationToken);

                while (ActiveLobbies.TryGetValue(sessionId, out lobby))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    lock (lobby.Lock)
                    {
                        if (lobby.Players.Count >= 4 || lobby.IsLaunched)
                            break;
                    }

                    var randomDelay = Random.Shared.Next(2000, 5000);
                    await Task.Delay(randomDelay, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    if (!ActiveLobbies.TryGetValue(sessionId, out lobby))
                        break;

                    List<Guid> currentLobbyPlayerIds;
                    lock (lobby.Lock)
                    {
                        if (lobby.Players.Count >= 4 || lobby.IsLaunched)
                            break;
                        currentLobbyPlayerIds = lobby.Players.Select(p => p.PlayerId).ToList();
                    }

                    var query = new GetRandomBotQuery(1, currentLobbyPlayerIds);

                    List<PlayerDto> dbBots;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        dbBots = await scopedMediator.Send(query, cancellationToken);
                    }

                    if (dbBots == null || !dbBots.Any())
                        break;

                    var botDto = dbBots.First();
                    bool botAdded = false;

                    lock (lobby.Lock)
                    {
                        if (lobby.Players.Count < 4 && !lobby.IsLaunched)
                        {
                            var assignedSide = lobby.Players.Count switch
                            {
                                0 => PlayerSide.South,
                                1 => PlayerSide.West,
                                2 => PlayerSide.North,
                                _ => PlayerSide.East
                            };

                            botDto.Side = assignedSide;
                            lobby.Players.Add(botDto);
                            botAdded = true;
                        }
                    }

                    if (botAdded)
                    {
                        await CheckAndLaunchGame(lobby, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"ℹ️ Ghost bot filler برای لابی {sessionId} لغو شد.");
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine($"ℹ️ Ghost bot filler برای لابی {sessionId} متوقف شد (Cts disposed).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطای غیرمنتظره در Ghost bot filler برای لابی {sessionId}: {ex.Message}");
            }
        }

        private async Task<Guid?> CheckAndLaunchGame(MatchmakingSession session, CancellationToken cancellationToken)
        {
            bool triggerLaunch = false;

            lock (session.Lock)
            {
                if (session.Players.Count == 4 && !session.IsLaunched)
                {
                    session.IsLaunched = true;
                    triggerLaunch = true;
                    ActiveLobbies.TryRemove(session.SessionId, out _);
                    try { session.Cts.Cancel(); } catch { }
                }
            }

            if (triggerLaunch)
            {
                return await LaunchGame(session);
            }
            else
            {
                var updateEvent = new GameEvent
                {
                    EventType = "player_joined",
                    Payload = JsonSerializer.Serialize(new
                    {
                        ConnectedCount = session.Players.Count,
                        Players = session.Players.Select(p => new
                        {
                            PlayerId = p.PlayerId.ToString(),
                            p.Name,
                            Side = p.Side.ToString(),
                            p.Avatar,
                            p.Level
                        }).ToList()
                    })
                };
                await _streamingService.BroadcastAsync(session.SessionId, updateEvent, cancellationToken);
                return null;
            }
        }

        public override async Task<LeaveLobbyResponse> LeaveLobby(LeaveLobbyRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.LobbyId, out var lobbyId) || !Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه لابی یا بازیکن نامعتبر است."));
            }

            PlayerActiveGames.TryRemove(playerId, out _);

            if (ActiveLobbies.TryGetValue(lobbyId, out var lobby))
            {
                bool shouldCancel = false;

                lock (lobby.Lock)
                {
                    lobby.Players.RemoveAll(p => p.PlayerId == playerId);
                    if (lobby.Players.Count == 0)
                    {
                        ActiveLobbies.TryRemove(lobbyId, out _);
                        shouldCancel = true;
                    }
                }

                if (shouldCancel)
                {
                    try { lobby.Cts.Cancel(); } catch { }
                    return new LeaveLobbyResponse { Success = true };
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

            return new LeaveLobbyResponse { Success = false };
        }

        private async Task<Guid> LaunchGame(MatchmakingSession session)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                try
                {
                    var startCmd = new StartGameCommand
                    {
                        Player1 = session.Players[0],
                        Player2 = session.Players[1],
                        Player3 = session.Players[2],
                        Player4 = session.Players[3],
                        TableKind = (Domain.Enums.TableKind)session.TableKind
                    };

                    List<Guid> humanPlayers = session.Players
                          .Select(p => p.PlayerId)
                          .ToList();

                    var requiredFee = GameConstants.GetTableFee((Domain.Enums.TableKind)session.TableKind);

                    if (requiredFee > 0 && humanPlayers.Any())
                    {
                        var deductCoinsCmd = new DeductCoinsCommand(humanPlayers, requiredFee);
                        await scopedMediator.Send(deductCoinsCmd, CancellationToken.None);
                    }

                    var actualGameId = await scopedMediator.Send(startCmd);

                    foreach (var player in session.Players)
                    {
                        PlayerActiveGames[player.PlayerId] = actualGameId;
                    }

                    // تشکیل تیم و توزیع اولیه کارت به صورت کنترل‌شده و ایمن در صف موتور بازی
                    var formTeamCmd = new FormTeamCommand { GameId = actualGameId };
                    await _coordinator.ExecuteAsync(actualGameId, formTeamCmd, CancellationToken.None);

                    var southPlayer = session.Players.First(p => p.Side == PlayerSide.South);
                    var dealCmd = new DealCardsCommand
                    {
                        DealerId = southPlayer.PlayerId,
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
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [CRITICAL LAUNCH ERROR] کرش فلو در زمان شروع بازی: {ex}");

                    var errorEvent = new GameEvent
                    {
                        EventType = "game_launch_failed",
                        Payload = JsonSerializer.Serialize(new
                        {
                            Reason = "launch_failed",
                            Message = "شروع بازی با خطای غیرمنتظره در سرور مواجه شد."
                        })
                    };
                    await _streamingService.BroadcastAsync(session.SessionId, errorEvent, CancellationToken.None);

                    throw;
                }
            }
        }

        public override async Task StreamGame(StreamRequest request, IServerStreamWriter<GameEvent> responseStream, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId) || !Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی یا بازیکن نامعتبر است."));
            }

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
                    var statusEvent = new GameEvent
                    {
                        EventType = "player_status_changed",
                        Payload = JsonSerializer.Serialize(new
                        {
                            PlayerId = playerId.ToString(),
                            IsOnline = true,
                            IsAutoPlay = false
                        })
                    };
                    await _streamingService.BroadcastAsync(gameId, statusEvent, context.CancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying player online status: {ex.Message}");
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
            }
            finally
            {
                _streamingService.Unsubscribe(subscription);
                await HandleGameDisconnect(gameId, playerId);
            }
        }

        public override async Task StreamLobby(StreamRequest request, IServerStreamWriter<GameEvent> responseStream, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var lobbyId) || !Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه لابی یا بازیکن نامعتبر است."));
            }

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

        private async Task HandleLobbyDisconnect(Guid lobbyId, Guid playerId)
        {
            Console.WriteLine($"=== [Lobby Disconnect] Player: {playerId} left Lobby: {lobbyId} ===");

            PlayerActiveGames.TryRemove(playerId, out _);

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
                    try { lobby.Cts.Cancel(); } catch { }
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

        public override async Task<GetTableConfigsResponse> GetTableConfigs(GetTableConfigsRequest request,ServerCallContext context)
        {
            var result = await _mediator.Send(new GetTableConfigsQuery(), context.CancellationToken);

            if (result.IsError)
            {
                var first = result.FirstError;
                throw new RpcException(new Status(StatusCode.Internal, first.Description));
            }

            var response = new GetTableConfigsResponse();
            response.Configs.AddRange(result.Value.Select(c => new TableConfigMessage
            {
                Kind = (TableKind)c.Kind,
                EntryFee = c.EntryFee,
                Prize = c.Prize,
                Rounds = c.Rounds,
                WinXp = c.WinXp,
                LossXp = c.LossXp,
            }));

            return response;
        }

        private async Task HandleGameDisconnect(Guid gameId, Guid playerId)
        {
            if (_streamingService.IsPlayerSubscribed(gameId, playerId))
            {
                return;
            }

            var statusEvent = new GameEvent
            {
                EventType = "player_status_changed",
                Payload = JsonSerializer.Serialize(new { PlayerId = playerId.ToString(), IsOnline = false, IsAutoPlay = true })
            };
            await _streamingService.BroadcastAsync(gameId, statusEvent, CancellationToken.None);

            var enableAutoPlayCmd = new EnableAutoPlayCommand(gameId, playerId);
            await _coordinator.ExecuteAsync(gameId, enableAutoPlayCmd, CancellationToken.None);

            await _coordinator.TryCleanupWorkerAsync(gameId);
        }

        public override async Task<ResumeControlResponse> ResumeControl(ResumeControlRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId) || !Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی یا بازیکن نامعتبر است."));
            }

            var cmd = new ResumeControlCommand(gameId, playerId);

            await _coordinator.ExecuteAsync(cmd.GameId, cmd, context.CancellationToken);

            return new ResumeControlResponse { Success = true };
        }

        public override async Task<InGameActionResponse> SendInGameAction(InGameActionRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی نامعتبر است."));
            }

            if (!Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازیکن نامعتبر است."));
            }

            if (!Guid.TryParse(request.ItemId, out var itemId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه آیتم نامعتبر است."));
            }

            if (PlayerActiveGames.TryGetValue(playerId, out var activeGameId) && activeGameId != gameId)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, "شما در این بازی حضور ندارید."));
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();

                var user = await userRepo.GetByIdAsync(playerId, context.CancellationToken);
                if (user == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "کاربر مورد نظر یافت نشد."));
                }

                var product = await productRepo.GetByItemIdAsync(itemId, context.CancellationToken);
                if (product == null || !product.IsActive)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "پک یا آیتم مربوطه یافت نشد یا غیرفعال است."));
                }

                var item = product.Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
                if (item == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "آیتم مورد نظر در پک یافت نشد."));
                }

                bool hasAccess = product.IsFree || user.OwnedProductIds.Contains(product.Id);
                if (!hasAccess)
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied, "شما مالک این استیکر یا متن چت نیستید."));
                }

                var actionEvent = new GameEvent
                {
                    EventType = "ingame_action",
                    Payload = JsonSerializer.Serialize(new
                    {
                        PlayerId = playerId.ToString(),
                        ActionType = product.ProductType.ToString(),
                        Content = item.Content
                    })
                };

                await _streamingService.BroadcastAsync(gameId, actionEvent, context.CancellationToken);
            }

            return new InGameActionResponse { Success = true };
        }

        public override async Task<FormTeamsResponse> FormTeams(FormTeamsRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی نامعتبر است."));
            }

            var cmd = new FormTeamCommand
            {
                GameId = gameId
            };
            var result = await _coordinator.ExecuteAsync(
                cmd.GameId,
                cmd,
                context.CancellationToken);
            return new FormTeamsResponse { GameId = result.GameId.ToString() };
        }

        public override async Task<DealCardsResponse> DealCards(DealCardsRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.DealerId, out var dealerId) || !Guid.TryParse(request.GameId, out var gameId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه دیلر یا بازی نامعتبر است."));
            }

            var cmd = new DealCardsCommand
            {
                DealerId = dealerId,
                GameId = gameId
            };
            await _coordinator.ExecuteAsync(
                cmd.GameId,
                cmd,
                context.CancellationToken);
            return new DealCardsResponse { Success = true };
        }

        public override async Task<PickTrumpResponse> PickTrump(PickTrumpRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.DealerId, out var dealerId) || !Guid.TryParse(request.GameId, out var gameId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه دیلر یا بازی نامعتبر است."));
            }

            if (!Enum.TryParse<Suit>(request.TrumpSuit, out var trumpSuit))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "نوع حکم نامعتبر است."));
            }

            var cmd = new PickTrumpCommand
            {
                DealerId = dealerId,
                GameId = gameId,
                TrumpSuit = trumpSuit
            };
            await _coordinator.ExecuteAsync(
                cmd.GameId,
                cmd,
                context.CancellationToken);
            return new PickTrumpResponse { Success = true };
        }

        public override async Task<ReadyToPlayResponse> ReadyToPlay(ReadyToPlayRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId) || !Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی یا بازیکن نامعتبر است."));
            }

            var cmd = new ReadyToPlayCommand(gameId, playerId);

            await _coordinator.ExecuteAsync(gameId, cmd, context.CancellationToken);

            return new ReadyToPlayResponse { Success = true };
        }

        public override async Task<PlayCardResponse> PlayCard(PlayCardRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId) || !Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی یا بازیکن نامعتبر است."));
            }

            if (!Enum.TryParse<Rank>(request.Rank, out var rank))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "رتبه کارت نامعتبر است."));
            }

            if (!Enum.TryParse<Suit>(request.Suit, out var suit))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "خال کارت نامعتبر است."));
            }

            var cmd = new PlayCardCommand
            {
                GameId = gameId,
                PlayerId = playerId,
                Rank = rank,
                Suit = suit
            };
            await _coordinator.ExecuteAsync(
                cmd.GameId,
                cmd,
                context.CancellationToken);
            return new PlayCardResponse { Success = true };
        }

        public override async Task<GameState> GetGameState(GetGameStateRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی نامعتبر است."));
            }

            var cmd = new GetGameStateQuery
            {
                GameId = gameId
            };
            var result = await _mediator.Send(cmd);
            return new GameState
            {
                GameId = result.GameId.ToString(),
                Status = result.Status.ToString(),
                CurrentRound = result.CurrentRound
            };
        }

        public override async Task<GetTrickDetailsResponse> GetTrickDetails(
     GetTrickDetailsRequest request,
     ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId) ||
                !Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی یا بازیکن نامعتبر است."));
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var user = await userRepo.GetByIdAsync(playerId, context.CancellationToken);

                if (user == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "کاربر مورد نظر یافت نشد."));
                }

                if (user.IsVip)
                {
                    throw new RpcException(new Status(StatusCode.PermissionDenied, "این قابلیت مخصوص کاربران VIP است."));
                }
            }

            var query = new GetTrickDetailsQuery(
                GameId: gameId,
                RequesterId: playerId,
                TrickIndex: request.TrickIndex,
                TeamId: request.TeamId
            );

            var result = await _mediator.Send(query, context.CancellationToken);

            return result.Match(
                success =>
                {
                    var response = new GetTrickDetailsResponse { Success = true };
                    response.Cards.AddRange(success.Cards.Select(c => new TrickCardMessage
                    {
                        Suit = c.Suit,
                        Rank = c.Rank,
                        PlayerId = c.PlayerId.ToString()
                    }));
                    return response;
                },
                errors =>
                {
                    var first = errors.First();
                    var status = first.Type switch
                    {
                        ErrorType.NotFound => StatusCode.NotFound,
                        ErrorType.Validation => StatusCode.InvalidArgument,
                        ErrorType.Forbidden => StatusCode.PermissionDenied,
                        _ => StatusCode.Internal
                    };
                    throw new RpcException(new Status(status, first.Description));
                }
            );
        }

        public override async Task<GetPlayerProfileResponse> GetPlayerProfile(GetPlayerProfileRequest request,ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId) ||
                !Guid.TryParse(request.PlayerId, out var requesterId) ||
                !Guid.TryParse(request.TargetPlayerId, out var targetPlayerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی یا بازیکن نامعتبر است."));
            }

            var query = new GetPlayerProfileQuery(gameId, requesterId, targetPlayerId);
            var result = await _mediator.Send(query, context.CancellationToken);

            return result.Match(
                success => new GetPlayerProfileResponse
                {
                    Success = true,
                    PlayerId = success.PlayerId.ToString(),
                    Name = success.Name,
                    Avatar = success.Avatar,
                    Border = success.Border ?? string.Empty,
                    Level = success.Level,
                    Coins = success.Coins,
                    IsVip = success.IsVip,
                    WonHands = success.WonHands,
                    LostHands = success.LostHands,
                },
                errors =>
                {
                    var first = errors.First();
                    throw new RpcException(new Status(
                        first.Type == ErrorOr.ErrorType.NotFound ? StatusCode.NotFound : StatusCode.Internal,
                        first.Description));
                }
            );
        }

        public override async Task<GameSnapshotResponse> GetSnapshot(GetSnapshotRequest request, ServerCallContext context)
        {
            if (!Guid.TryParse(request.GameId, out var gameId) || !Guid.TryParse(request.PlayerId, out var playerId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه بازی یا بازیکن نامعتبر است."));
            }

            var cmd = new GetGameSnapshotQuery
            {
                GameId = gameId,
                PlayerId = playerId
            };
            var result = await _mediator.Send(cmd);
            return new GameSnapshotResponse
            {
                Payload = JsonSerializer.Serialize(result)
            };
        }
    }
}