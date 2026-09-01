using Grpc.Core;
using Hokm.Application.Features.EquipProduct.Commands;
using Hokm.Application.Features.profile.Commands.UpdateProfile;
using Hokm.Application.Features.profile.Queries.GetProfile;
using MediatR;

namespace Hokm.Presentation.gRPC.Services
{
    public class UserGrpcService : UserService.UserServiceBase
    {
        private readonly IMediator _mediator;

        public UserGrpcService(IMediator mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        public override async Task<UserProfileResponse> GetProfile(GetProfileRequest request, ServerCallContext context)
        {
            try
            {
                // ✅ بهبود اعتبارسنجی
                if (!Guid.TryParse(request.UserId, out var userId))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه کاربر نامعتبر است."));
                }

                var query = new GetProfileQuery(userId);
                var result = await _mediator.Send(query, context.CancellationToken);

                if (result.IsError)
                {
                    var firstError = result.FirstError;
                    throw new RpcException(new Status(StatusCode.NotFound, firstError.Description));
                }

                var profile = result.Value;

                return new UserProfileResponse
                {
                    UserId = profile.Id.ToString(),
                    FullName = profile.FullName,
                    UserName = profile.UserName,
                    Email = profile.Email ?? "",
                    Coin = profile.Coin,
                    Level = profile.Level,
                    Score = profile.Score,
                    Wins = profile.Wins,
                    Loses = profile.Loses,
                    IsVip = profile.IsVip,
                    VipExpireDate = profile.VipExpireDate?.ToString("o") ?? "",
                    HasChangedName = profile.HasChangedName,
                    OwnedProductIds = { profile.OwnedProductIds.ConvertAll(id => id.ToString()) },
                    ActiveCardThemeId = profile.ActiveCardThemeId?.ToString() ?? "",
                    ActiveTableThemeId = profile.ActiveTableThemeId?.ToString() ?? "",
                    ActiveAvatarBorderId = profile.ActiveAvatarBorderId?.ToString() ?? ""
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<UpdateProfileResponse> UpdateProfile(UpdateProfileRequest request, ServerCallContext context)
        {
            try
            {
                // ✅ بهبود اعتبارسنجی
                if (!Guid.TryParse(request.UserId, out var userId))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه کاربر نامعتبر است."));
                }

                var command = new UpdateProfileCommand
                {
                    UserId = userId,
                    FullName = request.FullName,
                    AvatarRef = request.AvatarRef
                };

                var result = await _mediator.Send(command, context.CancellationToken);

                if (result.IsError)
                {
                    var firstError = result.FirstError;
                    throw new RpcException(new Status(StatusCode.InvalidArgument, firstError.Description));
                }

                bool isSuccess = result.Value.Success;

                return new UpdateProfileResponse { Status = isSuccess };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<EquipProductResponse> EquipProduct(EquipProductRequest request, ServerCallContext context)
        {
            try
            {
                // ✅ بهبود اعتبارسنجی
                if (!Guid.TryParse(request.UserId, out var userId))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه کاربر نامعتبر است."));
                }

                if (!Guid.TryParse(request.ProductId, out var productId))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "شناسه محصول نامعتبر است."));
                }

                var command = new EquipProductCommand(
                    userId,
                    productId,
                    (Domain.Enums.ProductType)request.ProductType
                );

                var result = await _mediator.Send(command, context.CancellationToken);

                if (result.IsError)
                {
                    var firstError = result.FirstError;
                    return new EquipProductResponse
                    {
                        Success = false,
                        Message = firstError.Description
                    };
                }

                return new EquipProductResponse
                {
                    Success = result.Value.Success,
                    Message = result.Value.Message
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }
    }
}