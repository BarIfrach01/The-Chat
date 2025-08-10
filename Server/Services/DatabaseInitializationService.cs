using Microsoft.Data.Sqlite;
using System.IO;

namespace Programmin2_classroom.Server.Services
{
    public class DatabaseInitializationService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(
            IConfiguration configuration,
            ILogger<DatabaseInitializationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitializeDatabaseAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                var databasePath = ExtractDatabasePath(connectionString);
                
                // יצירת התיקייה אם לא קיימת
                var directory = Path.GetDirectoryName(databasePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation($"Created database directory: {directory}");
                }

                // בדיקה אם בסיס הנתונים כבר קיים
                bool databaseExists = File.Exists(databasePath);

                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                if (!databaseExists)
                {
                    _logger.LogInformation("Database not found. Creating new database with initial schema...");
                    await CreateInitialSchema(connection);
                }
                else
                {
                    _logger.LogInformation("Database already exists. Checking schema...");
                    await EnsureSchemaExists(connection);
                }

                _logger.LogInformation("Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database initialization");
                throw;
            }
        }

        private string ExtractDatabasePath(string connectionString)
        {
            // חילוץ נתיב בסיס הנתונים מתוך connection string
            var builder = new SqliteConnectionStringBuilder(connectionString);
            return builder.DataSource;
        }

        private async Task CreateInitialSchema(SqliteConnection connection)
        {
            var sql = @"
                -- יצירת טבלת משתמשים
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT UNIQUE NOT NULL,
                    Password TEXT NOT NULL,
                    IsOnline BOOLEAN DEFAULT 0,
                    IsAdmin BOOLEAN DEFAULT 0,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                -- יצירת טבלת הודעות
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Text TEXT NOT NULL,
                    UserId INTEGER NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    LastModified DATETIME NULL,
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                -- יצירת טבלת לוגים
                CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Action TEXT NOT NULL,
                    TimeAction DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                -- אינדקסים
                CREATE INDEX IF NOT EXISTS idx_messages_created_at ON Messages(CreatedAt);
                CREATE INDEX IF NOT EXISTS idx_messages_userid ON Messages(UserId);
                CREATE INDEX IF NOT EXISTS idx_users_username ON Users(Username);
                CREATE INDEX IF NOT EXISTS idx_audit_logs_userid ON AuditLogs(UserId);
                CREATE INDEX IF NOT EXISTS idx_audit_logs_time ON AuditLogs(TimeAction);
            ";

            using var command = new SqliteCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            // יצירת משתמש אדמין ברירת מחדל
            await CreateDefaultAdminUser(connection);
        }

        private async Task EnsureSchemaExists(SqliteConnection connection)
        {
            // בדיקת קיום טבלאות ויצירתן במידת הצורך
            var tables = new[] { "Users", "Messages", "AuditLogs" };
            
            foreach (var table in tables)
            {
                var checkSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{table}';";
                using var checkCommand = new SqliteCommand(checkSql, connection);
                var result = await checkCommand.ExecuteScalarAsync();
                
                if (result == null)
                {
                    _logger.LogWarning($"Table {table} not found. Creating...");
                    await CreateInitialSchema(connection);
                    break;
                }
            }
        }

        private async Task CreateDefaultAdminUser(SqliteConnection connection)
        {
            // בדיקה אם יש כבר משתמש אדמין
            var checkAdminSql = "SELECT COUNT(*) FROM Users WHERE IsAdmin = 1;";
            using var checkCommand = new SqliteCommand(checkAdminSql, connection);
            var adminCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

            if (adminCount == 0)
            {
                // יצירת משתמש אדמין - סיסמה: admin123
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword("admin123");
                var createAdminSql = @"
                    INSERT INTO Users (Username, Password, IsAdmin, IsOnline) 
                    VALUES ('admin', @Password, 1, 0);
                ";
                
                using var command = new SqliteCommand(createAdminSql, connection);
                command.Parameters.AddWithValue("@Password", hashedPassword);
                await command.ExecuteNonQueryAsync();
                
                _logger.LogInformation("Default admin user created. Username: admin, Password: admin123");
            }
        }
    }
}