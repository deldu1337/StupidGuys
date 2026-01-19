using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Auth.Jwt
{
    public static class JwtUtils
    {
        const string SECRET_KEY = "unity_bootcamp_14_aslkfjlkadnvfkdfjnlaksdfnakjfd";
        readonly static byte[] KEY_BYTES = Encoding.UTF8.GetBytes(SECRET_KEY);
        public readonly static SymmetricSecurityKey SYM_KEY = new SymmetricSecurityKey(KEY_BYTES); // 대칭키
        public const string ISSUER = "AuthServer";
        public const string AUDIENCE = "GameClient";

        /// <summary>
        /// 토큰 생성
        /// </summary>
        /// <returns> JWT </returns>
        public static string Generate(string username, string userSessionId, TimeSpan lifetime)
        {
            var creds = new SigningCredentials(SYM_KEY, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username), // 사용자 식별
                new Claim("session_id", userSessionId) // 세션 식별
            };
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                    issuer: ISSUER,
                    audience: AUDIENCE,
                    claims: claims,
                    notBefore: now,
                    expires: now + lifetime,
                    signingCredentials: creds
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// 토큰 검증
        /// </summary>
        public static ClaimsPrincipal Validate(string jwt)
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = ISSUER,
                ValidateAudience = true,
                ValidAudience = AUDIENCE,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SYM_KEY,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5) // 서버들 간의 시스템상 시간 오차
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(jwt, parameters, out _);
        }
    }
}
