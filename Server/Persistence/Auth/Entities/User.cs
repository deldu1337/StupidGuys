namespace Auth.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string? Nickname { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastConnected { get; set; }
    }
}
