namespace SkRagIntentChatFunction
{
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel.ChatCompletion;
    using Microsoft.SemanticKernel;
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Azure.AI.OpenAI;
    using Microsoft.Azure.Functions.Worker.Http;
    using Microsoft.Azure.Functions.Worker;
    using Microsoft.SemanticKernel.Connectors.OpenAI;
    using System.IO;
    using SkRagIntentChatFunction.Models;
    
    public class ChatProvider
    {
        private readonly ILogger<ChatProvider> _logger;
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chat;
        private readonly ChatHistory _chatHistory;

        public ChatProvider(ILogger<ChatProvider> logger, Kernel kernel, IChatCompletionService chat, ChatHistory chatHistory)
        {
            _logger = logger;
            _kernel = kernel;
            _chat = chat;
            _chatHistory = chatHistory;
            // _kernel.ImportPluginFromObject(new TextAnalyticsPlugin(_client));
        }

        [Function("ChatProvider")]
        public async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            // Request body example:
            /*
                {
                    "userId": "stevesmith@contoso.com",
                    "sessionId": "12345678",
                    "tenantId": "00001",
                    "prompt": "Hello, What can you do for me?"
                }
            */

            _chatHistory.Clear();

            _logger.LogInformation("C# HTTP SentimentAnalysis trigger function processed a request.");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var chatRequest = JsonSerializer.Deserialize<ChatProviderRequest>(requestBody);
            if (chatRequest == null || chatRequest.userId == null || chatRequest.sessionId == null || chatRequest.tenantId == null || chatRequest.prompt == null)
            {
                throw new ArgumentNullException("Please check your request body, you are missing required data.");
            }

            var intent = await Util.GetIntent(_chat, chatRequest.prompt);

            // The purpose of using an Intent pattern is to allow you to make decisions about how you want to invoke the LLM.
            // In the case of RAG, if you can detect hte user intent is related to searching manuals, then you can perform
            // only that action when the intent is to search manuals. This allows you to reduce the token usage and
            // save you TPM and cost
            switch (intent)
            {
                case "manual":
                    {
                        Console.WriteLine("Intent: manual");

                        var function = _kernel.Plugins.GetFunction("AzureAISearchPlugin", "SearchManualsIndex");
                        var responseContent = await _kernel.InvokeAsync(function, new() { ["query"] = chatRequest.prompt });
                        _chatHistory.AddUserMessage(responseContent.ToString());
                        _chatHistory.AddUserMessage(chatRequest.prompt);

                        break;
                    }
                case "database":
                    {
                        // At this point we know the intent is database related so we could just call the plugin
                        // directly like the manuals above, but since we have AutoInvokeKernelFunctions enabled,
                        // we can just let SK detect that it needs to call the function and let it do it. However,
                        // it would be more performant to just call it directly as there is additional overhead
                        // with SK searching the plugin collection.
                        Console.WriteLine("Intent: database");

                        var dbSchema = Util.GetDatabasePrompt(false);

                        var systemPrompt = $@"You are a friendly AI assistant that responds to user queries from the company database table.
                                              The table contains organizational hierarchical position and employee details and the table schema is:
                                              {dbSchema}
                                              You are responsible for fetching the hierarchical position and employee detials using the appropriate plugin.
                                              You are also responsible for filtering data based on user input.
                                              User can request data using full or partial employee full name or job title.
                                              Give details in bullet points if possible.
                                              Summarize the provided data without using any additional external information or personal knowledge.";

                        _chatHistory.AddUserMessage(chatRequest.prompt);
                        break;
                    }
                case "not_found":
                    {
                        Console.WriteLine("Intent: not_found");
                        break;
                    }
            }

            ChatMessageContent result = null;

            if (!intent.Equals("!found"))
            {
                result = await _chat.GetChatMessageContentAsync
                    (
                        _chatHistory,
                        executionSettings: new OpenAIPromptExecutionSettings { Temperature = 0.8, TopP = 0.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions },
                        kernel: _kernel
                    );

                Console.WriteLine(result.Content);
            }



            // We are going to call the SearchPlugin to see if we get any hits on the query, if we do add them to the chat history and let AI summarize it 

            // var function = _kernel.Plugins.GetFunction("AzureAISearchPlugin", "SimpleHybridSearch"); 
            //var function = _kernel.Plugins.GetFunction("AzureAISearchPlugin", "SearchManualsIndex");

            //var responseContent = await _kernel.InvokeAsync(function, new() { ["query"] = chatRequest.prompt });

            //var promptTemplate = $"{responseContent.ToString()}\n Using the details above attempt to summarize or answer to the following question \n Question: {chatRequest.prompt} \n if you cannot complete the task using the above information, do not use external knowledge and simply state you cannot help with that question";
            //_chatHistory.AddMessage(AuthorRole.User, promptTemplate);

            // _chatHistory.AddMessage(AuthorRole.User, responseTest.ToString());
            // _chatHistory.AddMessage(AuthorRole.User, chatRequest.prompt);
            // _chatHistory.AddMessage(AuthorRole.System, "If the prompt cannot be answered by the AzureAISearchPlugin or the DBQueryPlugin, then simply ask for more details");

            // now it's time to use the Kernel to invoke our logic...
            // lets call the Chat Completion without using RAG for now...
            //var result = await _chat.GetChatMessageContentAsync(
            //        _chatHistory,
            //        executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 800, Temperature = 0.7, TopP = 0.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions },
            //        kernel: _kernel);

            // Add sample code to extract the token useage from the response
            // This is for example purposes, as it could be cached off to keep track of useage
            // SK does not have method to estiamte token count for a give prompt prior to sending to AI
            // so you could use the SharpToken Library of you wanted to check estimated the token size for a give prompt
            // by doing so you could impliment logic to reduce the size of the prompt to reduce the token count

            var metadata = result.Metadata;

            if (metadata != null && metadata.ContainsKey("Usage"))
            {
                var usage = (CompletionsUsage?)metadata["Usage"];
                Console.WriteLine($"Token usage. Input tokens: {usage?.PromptTokens}; Output tokens: {usage?.CompletionTokens}; Total tokens: {usage?.TotalTokens}");
            }


            //  var func = _kernel.Plugins.TryGetFunction("AzureAISearchPlugin","SimpleHybridSearch", out function);

            /*HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                string notFoundMessage = "Your question isn't related to materials I have indexed or related to database queries, so I am unable to help. Please" +
                    "ask a question related to documents I have indexed or something related to databases.";
                await response.WriteStringAsync(result.Content ?? notFoundMessage);
            }
            catch (Exception ex)
            {
                // Log exception details here
                Console.WriteLine(ex.Message);
                throw; // Re-throw the exception to propagate it further
            }*/

            return result.Content;
        }
    }
}