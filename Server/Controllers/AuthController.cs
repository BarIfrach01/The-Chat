using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Programmin2_classroom.Server.Data;
using Programmin2_classroom.Shared.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace Programmin2_classroom.Server.Controllers
{
    
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DbRepository _db;
        private readonly IConfiguration _configuration;

        public AuthController(DbRepository db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        // יצירת JWT Token
        private string GenerateJwtToken(string username, bool isAdmin)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"]);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User"),
                new Claim("username", username),
                new Claim("isAdmin", isAdmin.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(expirationMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // שיטת התחברות 
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDetails loginDto)
        {
            Console.WriteLine($"Login attempt for: {loginDto?.Username}");

            // בדיקת תקינות נתונים
            if (loginDto == null || string.IsNullOrWhiteSpace(loginDto.Username) ||
                string.IsNullOrWhiteSpace(loginDto.Password))
            {
                Console.WriteLine("Bad request - missing data");
                return BadRequest("שם משתמש וסיסמה נדרשים");
            }

            // שליפת פרטי המשתמש 
            string query = @"
                SELECT Username, Password, IsAdmin
                FROM Users
                WHERE Username = @Username
            ";

            var records = await _db.GetRecordsAsync<UserDbModel>(query, new { Username = loginDto.Username });
            var user = records?.FirstOrDefault();

            if (user == null)
            {
                Console.WriteLine("User not found");
                return Unauthorized("שם משתמש או סיסמה שגויים");
            }

            // בדיקת הסיסמה עם BCrypt
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password);
            if (!isPasswordValid)
            {
                Console.WriteLine("Invalid password");
                return Unauthorized("שם משתמש או סיסמה שגויים");
            }

            // עדכון סטטוס התחברות
            string updateQuery = "UPDATE Users SET IsOnline = 1 WHERE Username = @Username";
            await _db.SaveDataAsync(updateQuery, new { Username = loginDto.Username });

            // שמירת לוג התחברות
            await _db.AddAuditLog(loginDto.Username, "התחברות");

            // יצירת JWT Token
            string token = GenerateJwtToken(loginDto.Username, user.IsAdmin);

            // החזרת התוצאה עם הטוקן
            var result = new
            {
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                Token = token
            };

            Console.WriteLine("Login successful");
            return Ok(result);
        }

        // שיטת הרשמה 
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] LoginDetails registerDto)
        {
            // בדיקת תקינות נתונים
            if (registerDto == null || string.IsNullOrWhiteSpace(registerDto.Username) ||
                string.IsNullOrWhiteSpace(registerDto.Password))
            {
                return BadRequest("שם משתמש וסיסמה נדרשים");
            }

            // בדיקה שהסיסמה חזקה 
            if (registerDto.Password.Length < 6)
            {
                return BadRequest("הסיסמה חייבת להכיל לפחות 6 תווים");
            }

            // בדיקה אם המשתמש כבר קיים
            string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Username";
            var records = await _db.GetRecordsAsync<int>(checkQuery, new { Username = registerDto.Username });
            var userCount = records?.FirstOrDefault() ?? 0;

            if (userCount > 0)
            {
                return BadRequest("שם המשתמש כבר קיים");
            }

            // הצפנת הסיסמה עם BCrypt
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            // הוספת המשתמש החדש   
            string insertQuery = @"
                INSERT INTO Users (Username, Password, IsOnline, IsAdmin)
                VALUES (@Username, @Password, 0, 0)
            ";

            await _db.SaveDataAsync(insertQuery, new 
            { 
                Username = registerDto.Username, 
                Password = hashedPassword 
            });

            // שמירת לוג הרשמה
            await _db.AddAuditLog(registerDto.Username, "הרשמה");

            Console.WriteLine($"User {registerDto.Username} registered successfully");
            return Ok("נרשמת בהצלחה");
        }
    }
}