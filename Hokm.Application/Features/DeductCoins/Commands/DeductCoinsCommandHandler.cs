using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.DeductCoins.Commands
{
    public class DeductCoinsCommandHandler : IRequestHandler<DeductCoinsCommand, Unit>
    {
        private readonly IUserRepository _userRepository;

        public DeductCoinsCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Unit> Handle(DeductCoinsCommand request, CancellationToken cancellationToken)
        {
            var success = await _userRepository.DeductCoinsAsync(request.userIds, request.Amount, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException("موجودی سکه کافی نیست یا پروفایل کاربر یافت نشد.");
            }

            return Unit.Value;
        }
    }
}

