//using Auth.Jwt;
//using Auth.Entities;
//using Auth.Repositories;
//using Microsoft.AspNetCore.Mvc;
//using System.Threading.Tasks;

//namespace Auth.Controllers
//{
//    [ApiController]
//    [Route("auth")]
//    public class AuthController : Controller
//    {
//        public AuthController(IUserRepository userRepository)
//        {
//            _userRepository = userRepository;
//        }

//        IUserRepository _userRepository;
//        Dictionary<Guid, User> _loginSessions = new(); // <sessionId, user>

//        [HttpPost("login")]
//        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
//        {
//            var user = await _userRepository.GetByUserNameAsync(dto.id);

//            // 1. 유저 존재 여부 및 비밀번호 확인
//            if (user == null || user.Password.Equals(dto.pw) == false)
//                return Unauthorized();

//            // 2. [핵심] DB의 IsLoggedIn 필드로 중복 로그인 확인
//            if (user.IsLoggedIn)
//            {
//                // 이미 true라면 409 Conflict 반환 -> 유니티의 else if (409)가 실행됨
//                return Conflict(new { message = "This account is already logged in." });
//            }

//            // 3. 로그인 상태로 변경 및 DB 저장
//            user.IsLoggedIn = true;
//            user.LastConnected = DateTime.UtcNow;

//            await _userRepository.UpdateAsync(user); // Repository에 Update 기능이 있어야 함

//            Guid sessionId = Guid.NewGuid();
//            _loginSessions.Add(sessionId, user);
//            var jwt = JwtUtils.Generate(user.Id.ToString(), sessionId.ToString(), TimeSpan.FromHours(1));
//            string userId = user.Id.ToString();
//            return Ok(new { jwt, userId = user.Id.ToString(), user.Nickname });
//        }

//        [HttpPost("logout")]
//        public IActionResult Logout([FromBody] LogoutDTO dto)
//        {
//            var user = await _userRepository.GetByUserNameAsync(dto.id);

//            if (user == null)
//                return Unauthorized();

//            // 로그인 상태 해제 및 DB 저장
//            user.IsLoggedIn = false;
//            await _userRepository.UpdateAsync(user);

//            return Ok();
//        }
//    }

//    public record LoginDTO(string id, string pw);
//    public record LogoutDTO(string id);
//}

using Auth.Jwt;
using Auth.Entities;
using Auth.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent; // 쓰레드 안전을 위해 사용
using System.Linq;

namespace Auth.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly IUserRepository _userRepository;

        // 세션ID -> 사용자 정보
        private static readonly ConcurrentDictionary<Guid, User> _loginSessions = new();

        // 사용자ID -> 세션ID (중복 로그인 방지를 위한 단일 로그인 인덱스)
        private static readonly ConcurrentDictionary<string, Guid> _activeUsers
            = new(StringComparer.OrdinalIgnoreCase);

        public AuthController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var user = await _userRepository.GetByUserNameAsync(dto.id);

            // 유저 존재 여부 및 비밀번호 확인
            if (user == null || user.Password.Equals(dto.pw) == false)
                return Unauthorized();

            // [핵심] 원자적 TryAdd로 동시 요청에도 중복 로그인을 차단
            Guid sessionId = Guid.NewGuid();
            if (!_activeUsers.TryAdd(dto.id, sessionId))
            {
                return Conflict(new { message = "This account is already logged in (Memory Session)." });
            }

            // 세션 테이블 반영 실패 시 activeUsers 롤백
            if (!_loginSessions.TryAdd(sessionId, user))
            {
                _activeUsers.TryRemove(dto.id, out _);
                return StatusCode(500, new { message = "Failed to create login session." });
            }

            // 마지막 접속 시간만 DB 업데이트 (선택 사항)
            user.LastConnected = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            var jwt = JwtUtils.Generate(user.Id.ToString(), sessionId.ToString(), TimeSpan.FromHours(1));

            return Ok(new
            {
                jwt,
                userId = user.Id.ToString(),
                user.Nickname
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout([FromBody] LogoutDTO dto)
        {
            if (_activeUsers.TryRemove(dto.id, out var sessionId))
            {
                _loginSessions.TryRemove(sessionId, out _);
                return Ok();
            }

            // 혹시 인덱스와 세션 딕셔너리가 어긋난 경우를 대비한 fallback
            var sessionToRemove = _loginSessions.FirstOrDefault(x => x.Value.Username == dto.id);
            if (!sessionToRemove.Equals(default(KeyValuePair<Guid, User>)))
                _loginSessions.TryRemove(sessionToRemove.Key, out _);

            return Ok();
        }
    }

    public record LoginDTO(string id, string pw);
    public record LogoutDTO(string id);
}
