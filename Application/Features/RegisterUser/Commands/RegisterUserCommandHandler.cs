using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using MediatR;

namespace Hokm.Application.Features.RegisterUser.Commands
{
    public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Guid>
    {
        private readonly IUserRepository _userRepository;
        public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {
            var user = new User(request.FullName,request.Email,request.PhoneNumber,request.UserName);
            return await _userRepository.AddAsync(user);
        }
    }
}
