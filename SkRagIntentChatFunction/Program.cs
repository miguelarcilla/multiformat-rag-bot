    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.ChatCompletion;
    using SkRagIntentChatFunction.Interfaces;
    using Azure.Search.Documents.Indexes;
    using SkRagIntentChatFunction.Services;
    using SkRagIntentChatFunction.Plugins;

    
    string DeploymentName = Environment.GetEnvironmentVariable("ApiDeploymentName", EnvironmentVariableTarget.Process) ?? "";
    string AzureOpenAiEndpoint = Environment.GetEnvironmentVariable("OpenAiEndpoint", EnvironmentVariableTarget.Process) ?? "";
    string AzureOpenAiApiKey = Environment.GetEnvironmentVariable("OpenAiApiKey", EnvironmentVariableTarget.Process) ?? "";
    string AzureSearchEndpoint = Environment.GetEnvironmentVariable("SearchServiceEndpoint", EnvironmentVariableTarget.Process) ?? "";
    string AzureSearchKey = Environment.GetEnvironmentVariable("SearchServiceKey", EnvironmentVariableTarget.Process) ?? "";
    string EmbeddingModel = Environment.GetEnvironmentVariable("EmbeddingModel", EnvironmentVariableTarget.Process) ?? "";
    string CosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDbConnection", EnvironmentVariableTarget.Process) ?? string.Empty;

var host = new HostBuilder()
        .ConfigureFunctionsWebApplication()
        .ConfigureServices(services =>
        {
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();
            services.AddScoped<IAzureCosmosDbService>(s =>
            {
                var connectionString = CosmosDbConnection;
                return new AzureCosmosDbService(connectionString);
            });

            services.AddTransient<Kernel>(s =>
            {
                var builder = Kernel.CreateBuilder();
                builder.AddAzureOpenAIChatCompletion(
                    DeploymentName,
                    AzureOpenAiEndpoint,
                    AzureOpenAiApiKey
                    );
                builder.Services.AddSingleton<SearchIndexClient>(s =>
                {
                    string endpoint = AzureSearchEndpoint;
                    string apiKey = AzureSearchKey;
                    return new SearchIndexClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
                });

                // Custom AzureAISearchService to configure request parameters and make a request.
                builder.Services.AddSingleton<IAzureAISearchService, AzureAISearchService>();
                //builder.Services.AddSingleto(n(new AzureAIAssistantService(AzureOpenAiEndpoint, AzureOpenAiApiKey, DeploymentName));

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                builder.AddAzureOpenAITextEmbeddingGeneration(EmbeddingModel, AzureOpenAiEndpoint, AzureOpenAiApiKey);
                #pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                builder.Plugins.AddFromType<DBQueryPlugin>();
                builder.Plugins.AddFromType<AzureAISearchPlugin>();

                return builder.Build();
            });

            services.AddSingleton<IChatCompletionService>(sp =>
                sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());
            
            services.AddSingleton<ChatHistory>(s =>
            {
                var chathistory = new ChatHistory();
                return chathistory;
            });
        })
        .Build();

    host.Run();