using Auth.Entities;
using Auth.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Auth.Controllers
{
    [ApiController]
    [Route("user")]
    public class UserController : Controller
    {
        public UserController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        private readonly IUserRepository _userRepository;

        [HttpGet("getall")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var users = await _userRepository.GetAllAsync();
                var response = users.Select(user
                    => new UserResponseDTO(
                            id: user.Id,
                            username: user.Username,
                            nickname: user.Nickname,
                            lastConnected: user.LastConnected
                            )
                    );

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetUser(Guid id)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                    return NotFound(new { error = "Not found user." });

                var userDto = new UserResponseDTO(
                    id: user.Id,
                    username: user.Username,
                    nickname: user.Nickname,
                    lastConnected: user.LastConnected
                    );

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
        {
            if (dto == null) return BadRequest(new { error = "Invalid request data." });
            if (string.IsNullOrEmpty(dto.username))
                return BadRequest(new { error = "Username is necessary." });

            if (string.IsNullOrEmpty(dto.password))
                return BadRequest(new { error = "Password is necessary." });

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = dto.username,
                Password = dto.password,
                Nickname = null,
                CreatedAt = DateTime.UtcNow,
                LastConnected = DateTime.UtcNow
            };

            try
            {
                var users = await _userRepository.GetAllAsync();
                bool exist = users.Any(user => user.Username.Equals(dto.username)); // DB 에 이미 id 등록된거있는지

                if (exist)
                {
                    return Conflict(new { error = "Already registered username." });
                }
                else
                {
                    await _userRepository.InsertAsync(user);
                    await _userRepository.SaveAsync();

                    var response = new UserResponseDTO(
                            id: user.Id,
                            username: user.Username,
                            nickname: user.Nickname,
                            lastConnected: user.LastConnected
                        );

                    // user 데이터
                    return Created(string.Empty, new UserResponseDTO(user.Id, user.Username, user.Nickname, user.LastConnected));
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpPatch("{id:guid}/nickname")]
        public async Task<IActionResult> UpdateNickname(Guid id, [FromBody] UpdateNicknameDTO dto)
        {
            // 유저 존재유무
            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
                return NotFound(new { error = "User not found" });

            try
            {
                // 닉네임 중복검사
                var users = await _userRepository.GetAllAsync();
                var exist = users.Any(user => user.Nickname?.Equals(dto.nickname, StringComparison.OrdinalIgnoreCase) ?? false);

                if (exist)
                {
                    return Conflict(new { isExist = exist, message = "Nickname already exist." });
                }
                else
                {
                    user.Nickname = dto.nickname;
                    await _userRepository.UpdateAsync(user);
                    await _userRepository.SaveAsync();
                    return Ok(new { isExist = exist, message = "Updated nickname.", newNickname = user.Nickname });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id, [FromBody] DeleteUserDTO dto)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                    return NotFound(new { error = "Not found user." });

                if (user.Username.Equals(dto.username) == false)
                    return Unauthorized();

                if (user.Password.Equals(dto.password) == false)
                    return Unauthorized();

                await _userRepository.DeleteAsync(id);
                await _userRepository.SaveAsync();
                return Ok(new { message = "Deleted user." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }
    }

    public record UserResponseDTO(Guid id, string username, string? nickname, DateTime? lastConnected);
    public record CreateUserDTO(string username, string password);
    public record UpdateNicknameDTO(string nickname);
    public record DeleteUserDTO(string username, string password);
}
