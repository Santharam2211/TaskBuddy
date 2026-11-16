using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TimeBuddy.Backend.Data;
using TimeBuddy.Backend.DTOs;
using TimeBuddy.Backend.Models;

namespace TimeBuddy.Backend.Services
{
    public interface IAuthService
    {
        Task<(bool Success, string Message, AuthResponseDto? Data)> RegisterAsync(RegisterDto dto);
        Task<(bool Success, string Message, AuthResponseDto? Data)> LoginAsync(LoginDto dto);
        Task<UserDto?> GetUserProfileAsync(int userId);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<(bool Success, string Message, AuthResponseDto? Data)> RegisterAsync(RegisterDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            // Check duplicate email
            var existingUser = await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
            if (existingUser)
            {
                return (false, "An account with this email address already exists.", null);
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            var response = new AuthResponseDto
            {
                Token = token,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            };

            return (true, "Registration successful.", response);
        }

        public async Task<(bool Success, string Message, AuthResponseDto? Data)> LoginAsync(LoginDto dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (user == null)
            {
                return (false, "Invalid email or password.", null);
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return (false, "Invalid email or password.", null);
            }

            var token = GenerateJwtToken(user);

            var response = new AuthResponseDto
            {
                Token = token,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            };

            return (true, "Login successful.", response);
        }

        public async Task<UserDto?> GetUserProfileAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            return new UserDto
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email
            };
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var keyString = jwtSettings["Key"] ?? "TimeBuddySuperSecretSecretKeyForAuthenticationAndJwtTokenValidation2026!";
            var issuer = jwtSettings["Issuer"] ?? "TimeBuddyBackend";
            var audience = jwtSettings["Audience"] ?? "TimeBuddyClient";
            var expireDays = int.TryParse(jwtSettings["ExpireDays"], out int days) ? days : 7;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(expireDays),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
