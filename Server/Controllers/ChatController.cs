using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Programmin2_classroom.Server.Data;
using Programmin2_classroom.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Programmin2_classroom.Server.Hubs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Programmin2_classroom.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // כל הפעולות דורשות אימות
    public class ChatController : ControllerBase
    {
        private readonly DbRepository _db;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(DbRepository db, IHubContext<ChatHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        // פונקציה לשליפת שם המשתמש מהטוקן
        private string GetCurrentUsername()
        {
            return User.FindFirst("username")?.Value ?? User.Identity.Name ?? "";
        }

        // בדיקה אם המשתמש הנוכחי הוא אדמין
        private bool IsCurrentUserAdmin()
        {
            var isAdminClaim = User.FindFirst("isAdmin")?.Value;
            return bool.TryParse(isAdminClaim, out bool isAdmin) && isAdmin;
        }

        // טעינת רשימת הודעות
        [HttpGet("messages")]
        public async Task<ActionResult<List<MessageDetails>>> GetAllMessages()
        {
            string query = @"
                SELECT M.id, M.userId, M.text, U.username, M.createdAt
                FROM Messages M
                JOIN Users U ON M.userId = U.id
                ORDER BY M.createdAt
            ";

            var messages = await _db.GetRecordsAsync<MessageDetails>(query);
            return Ok(messages);
        }

        // שליפת כל המשתמשים עם סטטוס חיבור
        [HttpGet("users")]
        public async Task<ActionResult<List<UserStatus>>> GetAllUsers()
        {
            string query = @"
                SELECT username, isOnline
                FROM Users
                ORDER BY username
            ";

            var users = await _db.GetRecordsAsync<UserStatus>(query);
            return Ok(users);
        }

        // פונקציה לשמירת שם משתמש
        private async Task<string> GetUsernameById(int userId)
        {
            string query = "SELECT username FROM Users WHERE id = @UserId";
            var usernames = await _db.GetRecordsAsync<string>(query, new { UserId = userId });
            return usernames.FirstOrDefault() ?? "";
        }

        // שליפת UserId לפי שם משתמש
        private async Task<int> GetUserIdByUsername(string username)
        {
            string query = "SELECT id FROM Users WHERE username = @Username";
            var ids = await _db.GetRecordsAsync<int>(query, new { Username = username });
            return ids.FirstOrDefault();
        }

        // הוספת הודעה חדשה - עכשיו לוקח את המשתמש מהטוקן
        [HttpPost("add")]
        public async Task<IActionResult> AddMessage([FromBody] AddMessageSecure dto)
        {
            string currentUsername = GetCurrentUsername();
            if (string.IsNullOrEmpty(currentUsername))
                return Unauthorized("לא ניתן לזהות את המשתמש");

            int userId = await GetUserIdByUsername(currentUsername);
            if (userId == 0)
                return Unauthorized("משתמש לא נמצא");

            string query = @"
                INSERT INTO Messages (text, userId, createdAt)
                VALUES (@Text, @UserId, DATETIME('now'))
            ";
            
            await _db.SaveDataAsync(query, new { Text = dto.Text, UserId = userId });
            await _db.AddAuditLog(currentUsername, "הוספת הודעה");

            // עדכון לכל הלקוחות דרך SignalR
            await _hubContext.Clients.All.SendAsync("NotifyUpdate");

            return Ok("הודעה נוספה");
        }

        // עריכת הודעה קיימת
        [HttpPut("edit")]
        public async Task<IActionResult> EditMessage([FromBody] EditMessageSecure dto)
        {
            string currentUsername = GetCurrentUsername();
            if (string.IsNullOrEmpty(currentUsername))
                return Unauthorized("לא ניתן לזהות את המשתמש");

            int userId = await GetUserIdByUsername(currentUsername);
            if (userId == 0)
                return Unauthorized("משתמש לא נמצא");

            // בדיקה שההודעה שייכת למשתמש
            string checkQuery = "SELECT userId FROM Messages WHERE id = @MessageId";
            var records = await _db.GetRecordsAsync<int>(checkQuery, new { MessageId = dto.MessageId });
            var ownerId = records.FirstOrDefault();

            if (ownerId != userId)
                return Unauthorized("אין הרשאה לערוך הודעה זו");

            string updateQuery = @"
                UPDATE Messages
                SET text = @NewText, lastModified = DATETIME('now')
                WHERE id = @MessageId
            ";

            await _db.SaveDataAsync(updateQuery, new { NewText = dto.NewText, MessageId = dto.MessageId });
            await _db.AddAuditLog(currentUsername, "עריכת הודעה");

            // עדכון לכל הלקוחות דרך SignalR
            await _hubContext.Clients.All.SendAsync("NotifyUpdate");

            return Ok("הודעה נערכה");
        }

        // מחיקת הודעה
        [HttpDelete("delete/{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            string currentUsername = GetCurrentUsername();
            if (string.IsNullOrEmpty(currentUsername))
                return Unauthorized("לא ניתן לזהות את המשתמש");

            int userId = await GetUserIdByUsername(currentUsername);
            if (userId == 0)
                return Unauthorized("משתמש לא נמצא");

            // בדיקה שההודעה שייכת למשתמש
            string checkQuery = "SELECT userId FROM Messages WHERE id = @MessageId";
            var records = await _db.GetRecordsAsync<int>(checkQuery, new { MessageId = messageId });
            var ownerId = records.FirstOrDefault();

            if (ownerId != userId)
                return Unauthorized("אין הרשאה למחוק הודעה זו");

            string deleteQuery = "DELETE FROM Messages WHERE id = @MessageId";
            await _db.SaveDataAsync(deleteQuery, new { MessageId = messageId });
            await _db.AddAuditLog(currentUsername, "מחיקת הודעה");

            // עדכון לכל הלקוחות דרך SignalR
            await _hubContext.Clients.All.SendAsync("NotifyUpdate");

            return Ok("הודעה נמחקה");
        }

        // שמירת מזהה בהתאם לשם משתמש
        [HttpGet("getUserIdByName/{username}")]
        public async Task<ActionResult<int>> GetUserIdByName(string username)
        {
            string query = "SELECT id FROM Users WHERE username = @Username";
            var ids = await _db.GetRecordsAsync<int>(query, new { Username = username });
            int id = ids.FirstOrDefault();

            if (id == 0)
                return NotFound("User not found");

            return Ok(id);
        }

        // סינון הודעות
        [HttpPost("messages/filter")]
        public async Task<ActionResult<List<MessageDetails>>> FilterMessages([FromBody] MessageFilter filter)
        {
            string query = @"
                SELECT M.id, M.userId, M.text, U.username, M.createdAt
                FROM Messages M
                JOIN Users U ON M.userId = U.id
                WHERE M.createdAt >= @From AND M.createdAt <= @To
            ";

            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                query += " AND M.text LIKE @Text";
            }

            query += " ORDER BY M.createdAt";

            var parameters = new
            {
                Text = $"%{filter.Text}%",
                From = filter.From,
                To = filter.To
            };

            var messages = await _db.GetRecordsAsync<MessageDetails>(query, parameters);
            return Ok(messages);
        }

        // הצגת טבלת לוגים - רק לאדמין
        [HttpGet("audit")]
        [Authorize(Roles = "Admin")] // רק אדמין יכול לגשת
        public async Task<ActionResult<List<AuditLog>>> GetAuditLogs()
        {
            string logQuery = @"
                SELECT A.Id, U.Username, A.Action, A.TimeAction
                FROM AuditLogs A
                JOIN Users U ON A.UserId = U.Id
                ORDER BY A.TimeAction DESC
            ";

            var logs = await _db.GetRecordsAsync<AuditLog>(logQuery);
            return Ok(logs);
        }

        // התנתקות
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string currentUsername = GetCurrentUsername();
            if (string.IsNullOrEmpty(currentUsername))
                return Ok(); // אם לא מזוהה, פשוט מחזיר OK

            string query = "UPDATE Users SET IsOnline = 0 WHERE Username = @Username";
            await _db.SaveDataAsync(query, new { Username = currentUsername });

            // עדכון לכל הלקוחות דרך SignalR
            await _hubContext.Clients.All.SendAsync("NotifyUpdate");
            
            // שמירת לוג התנתקות
            await _db.AddAuditLog(currentUsername, "התנתקות");
            
            return Ok();
        }
    }
    
    public class AddMessageSecure
    {
        public string Text { get; set; }
    }


}