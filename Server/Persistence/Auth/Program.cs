using Auth;
using Auth.Jwt;
using Auth.Repositories;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Persistence
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<GameDbContext>(options =>
            {
                options.UseMySql(
                        builder.Configuration.GetConnectionString("Default"),
                        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Default"))
                    )
                    .EnableSensitiveDataLogging() // 개발 디버깅용
                    .EnableDetailedErrors(); // 개발 디버깅용
            });
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddControllers();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtUtils.ISSUER,
                    ValidateAudience = true,
                    ValidAudience = JwtUtils.AUDIENCE,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtUtils.SYM_KEY,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5) // 서버들 간의 시스템상 시간 오차
                };
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // 실행시마다 마이그레이션 (DBContext 구조를 기록해서 DB 에 적용하기위한 작업)
            using (var scope = app.Services.CreateScope())
            {
                var dbCtx = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                dbCtx.Database.Migrate();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
