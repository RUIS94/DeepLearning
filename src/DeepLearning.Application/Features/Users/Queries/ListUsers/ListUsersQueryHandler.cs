using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.Users.Queries.ListUsers
{
    public class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, ListUsersResult>
    {
        private readonly IUserRepository _userRepository;

        public ListUsersQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ListUsersResult> Handle(ListUsersQuery request, CancellationToken cancellationToken)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 200);

            var (items, totalCount) = await _userRepository.ListAsync((page - 1) * pageSize, pageSize, cancellationToken);

            return new ListUsersResult(
                items.Select(u => new ListUsersResultItem(
                    u.Id, u.Username, u.Email, u.DisplayName, u.Role, u.CreatedAt, u.LastLoginAt)).ToList(),
                totalCount, page, pageSize);
        }
    }
}
