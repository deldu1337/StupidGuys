using Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.Repositories
{
    public class UserRepository : IUserRepository
    {
        public UserRepository(GameDbContext context)
        {
            _context = context;
        }

        private GameDbContext _context;

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var user = await GetByIdAsync(id, ct);

            if (user == null)
                throw new InvalidOperationException("Failed to delete user. does not exist");

            _context.Users.Remove(user);
        }

        public async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct)
            => await _context.Users.ToListAsync(ct);

        public async Task<User> GetByIdAsync(Guid id, CancellationToken ct)
            => await _context.Users.FindAsync(id, ct);

        public async Task<User> GetByUserNameAsync(string username, CancellationToken ct)
            => await _context.Users.FirstOrDefaultAsync(u => u.Username.Equals(username), ct);

        public async Task InsertAsync(User user, CancellationToken ct)
            => _context.Users.AddAsync(user, ct);

        public Task SaveAsync(CancellationToken ct)
            => _context.SaveChangesAsync(ct);

        public async Task UpdateAsync(User user, CancellationToken ct)
        {
            var existingUser = await GetByIdAsync(user.Id, ct);

            if (existingUser == null)
                throw new InvalidOperationException($"User with id{user.Id} not found.");

            existingUser.Username = user.Username;
            existingUser.Password = user.Password;
            existingUser.Nickname = user.Nickname;
            existingUser.LastConnected = user.LastConnected;
            
            existingUser.SkinIndex = user.SkinIndex;

            _context.Users.Update(existingUser);
        }
    }
}
