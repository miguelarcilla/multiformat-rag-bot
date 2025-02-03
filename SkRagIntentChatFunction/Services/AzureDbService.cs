using Dapper;
using Newtonsoft.Json;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SkRagIntentChatFunction.Services
{
    public class AzureDbService
    {
        private readonly string _connectionString;

        public AzureDbService(string connectionString) {
            _connectionString = connectionString;
        }

        public async Task<string> GetDbResults(string query)
        {
            using IDbConnection connection = new SqlConnection(_connectionString);

            var dbResult = await connection.QueryAsync<dynamic>(query);
            var jsonString = JsonConvert.SerializeObject(dbResult);

            return jsonString;
        }
    }
}
