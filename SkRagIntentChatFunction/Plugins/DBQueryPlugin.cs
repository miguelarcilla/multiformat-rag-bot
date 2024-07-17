namespace SkRagIntentChatFunction.Plugins
{
    using Microsoft.SemanticKernel;
    using SkRagIntentChatFunction.Services;
    using System.ComponentModel;

    public class DBQueryPlugin
    {
        private static bool _hrToggleContact;
        private static string _dbConnection = Environment.GetEnvironmentVariable("DatabaseConnection", EnvironmentVariableTarget.Process) ?? string.Empty;

        [KernelFunction]
        [Description("Executes a SQL query to provide information about products.")]
        public static async Task<string> GetProductDetails(string query)
        {
            Console.WriteLine($"SQL Query: {query}");

            var azureDbService = new AzureDbService(_dbConnection);
            var dbResults = await azureDbService.GetDbResults(query);

            string results = dbResults;

            Console.WriteLine($"DB Results:{results}");
            return results;
        }

        [KernelFunction]
        [Description("Executes a SQL query to provide information about customers.")]
        public static async Task<string>GetCustomerDetails(string query)
        {
            Console.WriteLine($"SQL Query: {query}");
            var azureDbService = new AzureDbService(_dbConnection);
            var dbResults = await azureDbService.GetDbResults(query);

            string results = dbResults;

            Console.WriteLine($"DB Results:{results}");
            return results;
        }
    }
}