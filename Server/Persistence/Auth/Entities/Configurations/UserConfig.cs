using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Entities.Configurations
{
    /// <summary>
    /// 엔티티의 무결성 보장
    /// </summary>
    public class UserConfig : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // guid 필수 (Key 지정, PK: Primary Key.. Entity 쿼리시 식별하는 기준) 
            builder.HasKey(u => u.Id);

            // 유저이름 중복 방지 (Index 지정, 검색을 빨리하기위함..)
            builder.HasIndex(u => u.Username)
                .IsUnique(true);

            // 닉네임 중복 방지 (Index 지정, 검색을 빨리하기위함..)
            builder.HasIndex(u => u.Nickname)
                .IsUnique(true);

            // 닉네임 최대 12글자, 필수아님
            builder.Property(u => u.Nickname)
                .HasMaxLength(12)
                .IsRequired(false);

            // Created at 은 MySQL Timestamp 자동입력
            builder.Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)") // 6자리 us
                .IsRequired();

            builder.Property(u => u.LastConnected)
                .IsRequired(false); // default 값도없고 필수 요소도 아니면 nullable property로 타입정의해야함
        }
    }
}
