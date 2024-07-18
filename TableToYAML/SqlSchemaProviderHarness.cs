// Copyright (c) Microsoft. All rights reserved.
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SemanticKernel.Data.Nl2Sql.Library.Schema;

namespace SemanticKernel.Data.Nl2Sql.Harness;

/// <summary>
/// Harness for utilizing <see cref="SqlSchemaProvider"/> to capture live database schema
/// definitions: <see cref="SchemaDefinition"/>.
/// </summary>
public sealed class SqlSchemaProviderHarness
{
    public SqlSchemaProviderHarness()
    {

    }

    /// <summary>
    /// Reverse engineer live database (design-time task).
    /// </summary>
    /// <remarks>
    /// After testing with the sample data-sources, try one of your own!
    /// </remarks>
    public async Task ReverseEngineerSchemaAsync()
    {

        foreach (var dbKey in Harness.Configuration.GetSection("ConnectionStrings").GetChildren())
        {
            var conn = dbKey.Value;

            var desc = Harness.Configuration.GetSection("DatabaseDescriptions").GetSection("desc").Value;
            string[] tblNames = Harness.Configuration.GetSection("DatabaseDescriptions").GetSection(dbKey.Key).GetSection("tables").Value.Split(',');

            await this.CaptureSchemaYAMLAsync(dbKey.Key, conn, desc, tblNames).ConfigureAwait(false);
        }
    }

    public async Task<string> ReverseEngineerSchemaYAMLAsync(string[] tableNames)
    {
        string desc;
        string conn;
        string dbName;
        if (Harness.Configuration.GetSection("ConnectionStrings").Exists())
        {
            if (Harness.Configuration.GetSection("ConnectionStrings").GetChildren().Any())
            {
                IEnumerable<IConfigurationSection> dbKey = Harness.Configuration.GetSection("ConnectionStrings").GetChildren();

                dbName = dbKey.FirstOrDefault().Key;
                desc = Harness.Configuration.GetSection("DatabaseDescriptions")[dbKey.FirstOrDefault().Key];
                conn = dbKey.FirstOrDefault().Value; // Harness.Configuration.GetSection("ConnectionStrings").Value;

                return await this.CaptureSchemaYAMLAsync(dbName, conn, desc, tableNames).ConfigureAwait(false);
            }
        }

        return string.Empty;
        // TODO: Reverse engineer your own database (comment-out others)
        //       Pass in optional 'tableNames' parameter to limit which tables or views are described.
    }

    public async Task<string> ReverseEngineerSchemaJSONAsync(string[] tableNames)
    {
        string desc;
        string conn;
        string dbName;
        if (Harness.Configuration.GetSection("ConnectionStrings").Exists())
        {
            if (Harness.Configuration.GetSection("ConnectionStrings").GetChildren().Any())
            {
                IEnumerable<IConfigurationSection> dbKey = Harness.Configuration.GetSection("ConnectionStrings").GetChildren();

                dbName = dbKey.FirstOrDefault().Key;
                desc = Harness.Configuration.GetSection("DatabaseDescriptions")[dbKey.FirstOrDefault().Key];
                conn = dbKey.FirstOrDefault().Value; // Harness.Configuration.GetSection("ConnectionStrings").Value;

                return await this.CaptureSchemaJSONAsync(dbName, conn, desc, tableNames).ConfigureAwait(false);
            }
        }

        return string.Empty;
        // TODO: Reverse engineer your own database (comment-out others)
        //       Pass in optional 'tableNames' parameter to limit which tables or views are described.
    }

    private async Task<string> CaptureSchemaYAMLAsync(string databaseKey, string? connectionString, string? description, params string[] tableNames)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var provider = new SqlSchemaProvider(connection);

        var schema = await provider.GetSchemaAsync(databaseKey, description, tableNames).ConfigureAwait(false);

        await connection.CloseAsync().ConfigureAwait(false);

        var yamlText = await schema.FormatAsync(YamlSchemaFormatter.Instance).ConfigureAwait(false);
        

        return yamlText;


        //// If you want to save to a file
        
        //await this.SaveSchemaAsync("yaml", databaseKey, yamlText).ConfigureAwait(false);
 
        //await this.SaveSchemaAsync("json", databaseKey, schema.ToJson()).ConfigureAwait(false);
    }

    private async Task<string> CaptureSchemaJSONAsync(string databaseKey, string? connectionString, string? description, params string[] tableNames)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var provider = new SqlSchemaProvider(connection);

        var schema = await provider.GetSchemaAsync(databaseKey, description, tableNames).ConfigureAwait(false);

        await connection.CloseAsync().ConfigureAwait(false);

        var yamlText = await schema.FormatAsync(YamlSchemaFormatter.Instance).ConfigureAwait(false);

        return schema.ToJson();

        //// If you want to save to a file

        //await this.SaveSchemaAsync("yaml", databaseKey, yamlText).ConfigureAwait(false);

        //await this.SaveSchemaAsync("json", databaseKey, schema.ToJson()).ConfigureAwait(false);
    }

    private async Task SaveSchemaAsync(string extension, string databaseKey, string schemaText)
    {
        var fileName = Path.Combine(Repo.RootConfigFolder, $"{databaseKey}.{extension}");

        using var streamCompact =
            new StreamWriter(
                fileName,
                new FileStreamOptions
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.Create,
                });

        await streamCompact.WriteAsync(schemaText).ConfigureAwait(false);
    }
}
