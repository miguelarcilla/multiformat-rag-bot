//using SemanticKernel.Data.Nl2Sql.Library.Schema;
using SemanticKernel.Data.Nl2Sql.Harness;

SqlSchemaProviderHarness ssph = new SqlSchemaProviderHarness();


string[] tableNames = "dbo.BuildVersion,dbo.ErrorLog".Split(",");
string yaml = await ssph.ReverseEngineerSchemaYAMLAsync(tableNames);
string json = await ssph.ReverseEngineerSchemaJSONAsync(tableNames);

Console.WriteLine("Hello, World!");
