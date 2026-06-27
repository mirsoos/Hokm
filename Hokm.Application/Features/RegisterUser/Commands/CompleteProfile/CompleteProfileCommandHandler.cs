using ErrorOr;
using Hokm.Application.DTOs.Auth;
using Hokm.Application.Interfaces;
using Hokm.Domain.Entities;
using MediatR;
using Hokm.Infrastructure.Services.Redis.Interfaces;

namespace Hokm.Application.Features.RegisterUser.Commands.CompleteProfile
{
    public class CompleteProfileCommandHandler : IRequestHandler<CompleteProfileCommand, ErrorOr<AuthResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IRedisCacheService _cacheService;

        public CompleteProfileCommandHandler(IUserRepository userRepository, IJwtTokenGenerator jwtTokenGenerator, IRedisCacheService cacheService)
        {
            _userRepository = userRepository;
            _jwtTokenGenerator = jwtTokenGenerator;
            _cacheService = cacheService;
        }

        public async Task<ErrorOr<AuthResponse>> Handle(CompleteProfileCommand request, CancellationToken cancellationToken)
        {
            var verifiedKey = $"verified-phone:{request.PhoneNumber}";
            var isVerified = await _cacheService.GetAsync<string>(verifiedKey, cancellationToken);

            if (string.IsNullOrEmpty(isVerified))
            {
                return Error.Unauthorized(code: "Auth.NotVerified", description: "ابتدا باید شماره موبایل خود را تایید کنید.");
            }

            if (await _userRepository.ExistsUserNameAsync(request.UserName, cancellationToken))
            {
                return Error.Conflict(code: "User.Duplicate", description: "این نام کاربری قبلا ثبت شده است.");
            }

            var user = new User(request.FullName, request.Email, request.PhoneNumber, request.UserName);
            await _userRepository.AddAsync(user, cancellationToken);

            await _cacheService.RemoveAsync(verifiedKey, CancellationToken.None);

            var token = _jwtTokenGenerator.GenerateToken(user.Id, user.PhoneNumber, user.UserName);

            return new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                UserName = user.UserName
            };
        }
    }
}