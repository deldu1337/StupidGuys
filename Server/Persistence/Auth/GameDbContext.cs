using Auth.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth
{
    /// <summary>
    /// DB 에 쿼리하기위한 흐름을 가지고있음 (DB Facade 등)
    /// </summary>
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions options)
            : base(options)
        {
        }

        /// <summary>
        /// DB 에서 User 엔티티 쿼리 루트
        /// </summary>
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // GameDbContext 타입이 정의된 어셈블리에서 IEntityTypeConfiguration 전부 찾아서 적용
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);

            // 리플렉션 안하려면..
            //modelBuilder.Entity<User>(e =>
            //{
            //    // guid 필수 (Key 지정, PK: Primary Key.. Entity 쿼리시 식별하는 기준) 
            //    e.HasKey(u => u.Id);
            //
            //    // 유저이름 중복 방지 (Index 지정, 검색을 빨리하기위함..)
            //    e.HasIndex(u => u.Username)
            //        .IsUnique(true);
            //
            //    // 닉네임 중복 방지 (Index 지정, 검색을 빨리하기위함..)
            //    e.HasIndex(u => u.Nickname)
            //        .IsUnique(true);
            //
            //    // 닉네임 최대 12글자, 필수아님
            //    e.Property(u => u.Nickname)
            //        .HasMaxLength(12)
            //        .IsRequired(false);
            //
            //    // Created at 은 MySQL Timestamp 자동입력
            //    e.Property(u => u.CreatedAt)
            //        .HasDefaultValueSql("CURRENT_TIMESTAMP(6)") // 6자리 us
            //        .IsRequired();
            //
            //    e.Property(u => u.LastConnected)
            //        .IsRequired(false); // default 값도없고 필수 요소도 아니면 nullable property로 타입정의해야함
            //});
        }
    }
}
