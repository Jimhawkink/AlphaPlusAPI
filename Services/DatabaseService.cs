using System.Data;
using Microsoft.Data.SqlClient;

namespace AlphaPlusAPI.Services  // ← Changed from AlphaPlusAPI.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<DataTable> ExecuteQueryAsync(string query, SqlParameter[]? parameters = null)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(query, connection);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            var dataTable = new DataTable();
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            dataTable.Load(reader);

            return dataTable;
        }

        public async Task<int> ExecuteNonQueryAsync(string query, SqlParameter[]? parameters = null)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(query, connection);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();
            return await command.ExecuteNonQueryAsync();
        }

        public async Task<object?> ExecuteScalarAsync(string query, SqlParameter[]? parameters = null)
        {
            using var connection = GetConnection();
            using var command = new SqlCommand(query, connection);

            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }

            await connection.OpenAsync();
            return await command.ExecuteScalarAsync();
        }
    }
}