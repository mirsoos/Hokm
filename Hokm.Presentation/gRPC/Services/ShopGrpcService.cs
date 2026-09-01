using Grpc.Core;
using Hokm.Application.Features.GetProducts.Queries;
using Hokm.Application.Features.InitiatePayment.Commands;
using Hokm.Application.Features.PurchaseWithCoins.Commands;
using MediatR;

namespace Hokm.Presentation.gRPC.Services
{
    public class ShopGrpcService : ShopService.ShopServiceBase
    {
        private readonly IMediator _mediator;

        public ShopGrpcService(IMediator mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        }

        public override async Task<GetProductsResponse> GetProducts(GetProductsRequest request, ServerCallContext context)
        {
            try
            {
                Domain.Enums.ProductType? filterType = request.HasFilterType
                    ? (Domain.Enums.ProductType)request.FilterType
                    : null;

                var query = new GetProductsQuery(filterType);
                var result = await _mediator.Send(query, context.CancellationToken);

                if (result.IsError)
                {
                    var firstError = result.FirstError;
                    throw new RpcException(new Status(StatusCode.Internal, firstError.Description));
                }

                var response = new GetProductsResponse();
                foreach (var prod in result.Value)
                {
                    response.Products.Add(new ProductMessage
                    {
                        Id = prod.Id.ToString(),
                        Title = prod.Title,
                        Description = prod.Description ?? "",
                        AssetKey = prod.AssetKey ?? "",
                        ProductType = (ProductType)prod.ProductType,
                        PaymentType = (PaymentType)prod.PaymentType,
                        Price = prod.Price,
                        CoinAmount = prod.CoinAmount ?? 0,
                        VipDurationDays = prod.VipDurationDays ?? 0,
                        IsFree = prod.IsFree
                    });
                }

                return response;
            }
            catch (Exception ex) when (!(ex is RpcException))
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<PurchaseWithCoinsResponse> PurchaseWithCoins(PurchaseWithCoinsRequest request, ServerCallContext context)
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

                var command = new PurchaseWithCoinsCommand(userId, productId);

                var result = await _mediator.Send(command, context.CancellationToken);

                if (result.IsError)
                {
                    var firstError = result.FirstError;
                    return new PurchaseWithCoinsResponse
                    {
                        Success = false,
                        Message = firstError.Description,
                        RemainingCoins = 0
                    };
                }

                return new PurchaseWithCoinsResponse
                {
                    Success = result.Value.Success,
                    Message = result.Value.Message,
                    RemainingCoins = result.Value.RemainingCoins
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

        public override async Task<InitiatePaymentResponse> InitiatePayment(InitiatePaymentRequest request, ServerCallContext context)
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

                var command = new InitiatePaymentCommand(
                    userId,
                    productId,
                    (Domain.Enums.GatewayType)request.Gateway
                );

                var result = await _mediator.Send(command, context.CancellationToken);

                if (result.IsError)
                {
                    var firstError = result.FirstError;
                    return new InitiatePaymentResponse
                    {
                        Success = false,
                        Message = firstError.Description,
                        TransactionId = "",
                        PaymentUrlOrToken = ""
                    };
                }

                return new InitiatePaymentResponse
                {
                    Success = result.Value.Success,
                    TransactionId = result.Value.TransactionId.ToString(),
                    PaymentUrlOrToken = result.Value.PaymentUrlOrToken,
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