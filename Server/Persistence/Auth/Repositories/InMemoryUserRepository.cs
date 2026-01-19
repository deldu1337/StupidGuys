/*
using Auth.Entities;

namespace Auth.Repositories
{
    public class InMemoryUserRepository : IUserRepository
    {
        Dictionary<Guid, User> _db = new()
        {
            { Guid.NewGuid(), new User { Username = "Tester", Password = "0000" } }
        };

        public void Delete(Guid id)
        {
            _db.Remove(id);
        }

        public IEnumerable<User> GetAll()
        {
            return _db.Values;
        }

        public User GetById(Guid id)
        {
            if (_db.TryGetValue(id, out User user))
                return user;

            return null;
        }

        public User GetByUserName(string username)
        {
            return _db.FirstOrDefault(pair => pair.Value.Username == username).Value;
        }

        public void Insert(User user)
        {
            _db.Add(user.Id, user);
        }

        public void Save()
        {
            // nothing to do
        }

        public void Update(User user)
        {
            if (_db.ContainsKey(user.Id))
                _db[user.Id] = user;

            throw new Exception();
        }
    }
}
*/