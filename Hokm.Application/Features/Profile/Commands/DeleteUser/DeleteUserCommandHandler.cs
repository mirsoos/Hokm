// Application/Features/Profile/Commands/DeleteUser/DeleteUserCommandHandler.cs

using ErrorOr;
using Hokm.Application.Interfaces;
using MediatR;

namespace Hokm.Application.Features.Profile.Commands.DeleteUser
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, ErrorOr<bool>>
    {
        private readonly IUserRepository _userRepository;

        public DeleteUserCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ErrorOr<bool>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var userExist = await _userRepository.ExistByIdAsync(request.UserId, cancellationToken);
            if (!userExist)
            {
                return Error.NotFound(
                    code: "User.NotFound",
                    description: "کاربر مورد نظر در سیستم یافت نشد."
                );
            }

            var deleteUser = await _userRepository.DeleteByIdAsync(request.UserId, cancellationToken);
            if (deleteUser)
            {
                return true;
            }

            return Error.Failure(
                code: "User.DeleteFailed",
                description: "عملیات حذف حساب کاربری در سرور با خطا مواجه شد."
            );
        }
    }
}