using Auth.Entities;

namespace Auth.Repositories
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default);
        Task<User> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<User> GetByUserNameAsync(string username, CancellationToken ct = default);
        Task InsertAsync(User user, CancellationToken ct = default);
        Task UpdateAsync(User user, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
    }
}
