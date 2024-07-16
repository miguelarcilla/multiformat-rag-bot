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

    var host = new HostBuilder()
        .ConfigureFunctionsWebApplication()
        .ConfigureServices(services =>
        {
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();

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

                #pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                builder.AddAzureOpenAITextEmbeddingGeneration(EmbeddingModel, AzureOpenAiEndpoint, AzureOpenAiApiKey);
                #pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                builder.Plugins.AddFromType<DBQueryPlugin>();
                builder.Plugins.AddFromType<AzureAISearchPlugin>();

                return builder.Build();
            });

            services.AddSingleton<IChatCompletionService>(sp =>
                sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());
            const string systemmsg = "You are a helpful friendly assistant that has knowledge of Org Builder Manuals.  You also have the ability to perform Org Build Database queries.  Do not answer any questions related to custom plugins or anything that is not related to the manuals or querying of the Org Builder Database.";
            services.AddSingleton<ChatHistory>(s =>
            {
                var chathistory = new ChatHistory();
                chathistory.AddSystemMessage(systemmsg);
                return chathistory;
            });
        })
        .Build();

    host.Run();