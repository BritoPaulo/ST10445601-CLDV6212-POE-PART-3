using Microsoft.EntityFrameworkCore;
using ABCRetailers.Data;
using ABCRetailers.Models.SQL;
using ABCRetailers.Models.ViewModels;
using System.Security.Cryptography;
using System.Text;

namespace ABCRetailers.Services
{
    public interface IAuthService
    {
        Task<SqlUser?> AuthenticateAsync(string username, string password);
        Task<SqlUser> RegisterAsync(RegisterViewModel model);
        Task<bool> UsernameExistsAsync(string username);
        Task<bool> EmailExistsAsync(string email);
        string HashPassword(string password);
    }

    public class AuthService : IAuthService
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AuthDbContext context, ILogger<AuthService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SqlUser?> AuthenticateAsync(string username, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => (u.Username == username || u.Email == username) && u.IsActive);

                if (user == null)
                    return null;

                var passwordHash = HashPassword(password);
                if (user.PasswordHash == passwordHash)
                {
                    user.LastLogin = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating user: {Username}", username);
                return null;
            }
        }

        public async Task<SqlUser> RegisterAsync(RegisterViewModel model)
        {
            try
            {
                _logger.LogInformation("Starting registration for user: {Username} as {Role}", model.Username, model.Role);

                var user = new SqlUser
                {
                    UserId = Guid.NewGuid(),
                    Username = model.Username,
                    Email = model.Email,
                    PasswordHash = HashPassword(model.Password),
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = model.Role, // Use the selected role from the form
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                _logger.LogInformation("Adding {Role} user to database: {Username}", user.Role, user.Username);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ {Role} user registered successfully: {Username} with ID {UserId}",
                    user.Role, user.Username, user.UserId);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for user: {Username}", model.Username);
                throw;
            }
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        public string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}