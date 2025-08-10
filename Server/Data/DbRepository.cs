using System;
using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Programmin2_classroom.Server.Data
{
	public class DbRepository
	{
        private IDbConnection _dbConnection;

        public DbRepository(IConfiguration config)
        {
            _dbConnection = new SqliteConnection(config.GetConnectionString("DefaultConnection"));
        }


        public void OpenConnection()
        {
            //if there is no connection to the DB
            if (_dbConnection.State != ConnectionState.Open)
            {
                //Create new connection to the db
                _dbConnection.Open();
            }
        }

        public void CloseConnection()
        {
            _dbConnection.Close();
        }

        //parameters = query parameters
        public async Task<IEnumerable<T>> GetRecordsAsync<T>(string query, object parameters = null)
        {
            try
            {
                OpenConnection();
                parameters ??= new { };
                IEnumerable<T> records = await _dbConnection.QueryAsync<T>(query, parameters, commandType: CommandType.Text);
                return records;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetRecordsAsync error: {ex.Message}");
                throw;
            }
            finally
            {
                CloseConnection();
            }
        }

        public async Task<int> SaveDataAsync(string query, object parameters = null)
        {
            try
            {
                OpenConnection();
                parameters ??= new { };
                int records = await _dbConnection.ExecuteAsync(query, parameters, commandType: CommandType.Text);
                return records;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveDataAsync error: {ex.Message}");
                throw;
            }
            finally
            {
                CloseConnection();
            }
        }

        public async Task<int> InsertReturnIdAsync(string query, object parameters = null)
        {
            try
            {
                OpenConnection();

                if (parameters == null) parameters = new { };

                int results = await _dbConnection.ExecuteAsync(sql: query, param: parameters, commandType: CommandType.Text);

                if (results > 0)
                {
                    int Id = _dbConnection.Query<int>("SELECT last_insert_rowid()").FirstOrDefault();
                    CloseConnection();
                    return Id;
                }
                CloseConnection();
                return 0;
            }
            catch (System.Exception)
            {
                CloseConnection();
                //return null;
                throw;

            }
        }
        public async Task AddAuditLog(string username, string action)
        {
            // קודם נמצא את ה-UserId לפי השם
            string getUserIdQuery = "SELECT Id FROM Users WHERE Username = @Username";
            var userIds = await GetRecordsAsync<int>(getUserIdQuery, new { Username = username });
            int userId = userIds.FirstOrDefault();
    
            if (userId == 0)
                return; // אם לא נמצא משתמש, לא נשמור לוג

            string query = @"
        INSERT INTO AuditLogs (Userid, Action, TimeAction)
        VALUES (@UserId, @Action, DATETIME('now'))
    ";

            await SaveDataAsync(query, new { UserId = userId, Action = action });
        }

   
    }
}

