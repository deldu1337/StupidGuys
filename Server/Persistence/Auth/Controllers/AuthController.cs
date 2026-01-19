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
//        public async Task<IActionResult> Logout([FromBody] LogoutDTO dto)
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

        // 1. static으로 선언해야 컨트롤러 인스턴스가 바뀌어도 세션이 유지됩니다.
        // 2. 여러 요청이 동시에 들어올 때 안전하도록 ConcurrentDictionary 사용을 권장합니다.
        private static readonly ConcurrentDictionary<Guid, User> _loginSessions = new();

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

            // [핵심] 딕셔너리의 Value들 중 현재 로그인하려는 유저의 ID가 있는지 확인
            var alreadyLoggedIn = _loginSessions.Values.Any(u => u.Username == dto.id);

            if (alreadyLoggedIn)
            {
                // 이미 세션 딕셔너리에 존재하면 409 Conflict 반환
                return Conflict(new { message = "This account is already logged in (Memory Session)." });
            }

            // 로그인 성공 처리
            Guid sessionId = Guid.NewGuid();

            // 메모리 세션에 추가
            _loginSessions.TryAdd(sessionId, user);

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
        public async Task<IActionResult> Logout([FromBody] LogoutDTO dto)
        {
            // 딕셔너리에서 해당 Username을 가진 세션을 찾아 제거
            var sessionToRemove = _loginSessions.FirstOrDefault(x => x.Value.Username == dto.id);

            if (!sessionToRemove.Equals(default(KeyValuePair<Guid, User>)))
            {
                bool result = _loginSessions.TryRemove(sessionToRemove.Key, out _);
            }

            return Ok();
        }
    }

    public record LoginDTO(string id, string pw);
    public record LogoutDTO(string id);
}