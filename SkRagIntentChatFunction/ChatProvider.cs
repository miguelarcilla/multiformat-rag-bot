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

            // if the intent has "-image" appended, we will need to use assistant API to generate the image
            // once the response is received, so determine if this is part of the intent, then strip it off
            // so we can process the root intent
            bool renderImageWithResponse = intent.EndsWith("-image");

            if (renderImageWithResponse) {
                intent = intent.Substring(0, intent.Length - "-image".Length);
            }

            // all database intents will the same thing, so in the switch statement we'll build the
            // schemas based on the type of database intent and then build and add the system and
            // user messages to the chat history outside of the switch so it isn't duplicated
            bool databaseIntent = false;

            var dbSchema = "";
            // TODO: utilize NL2SQL to get only tables related to products
            var jsonSchema = "";

            // The purpose of using an Intent pattern is to allow you to make decisions about how you want to invoke the LLM.
            // In the case of RAG, if you can detect the user intent is related to searching manuals, then you can perform
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
                case "databaseproduct":
                    {
                        databaseIntent = true;

                        // At this point we know the intent is database related so we could just call the plugin
                        // directly like the manuals above, but since we have AutoInvokeKernelFunctions enabled,
                        // we can just let SK detect that it needs to call the function and let it do it. However,
                        // it would be more performant to just call it directly as there is additional overhead
                        // with SK searching the plugin collection.
                        Console.WriteLine("Intent: databaseproduct");

                        dbSchema = Util.GetDatabaseSchema();
                        // TODO: utilize NL2SQL to get only tables related to products
                        jsonSchema = Util.GetDatabaseJsonSchema(false);
                        
                        break;
                    }
                case "databasecustomer":
                    {
                        databaseIntent = true;

                        // At this point we know the intent is database related so we could just call the plugin
                        // directly like the manuals above, but since we have AutoInvokeKernelFunctions enabled,
                        // we can just let SK detect that it needs to call the function and let it do it. However,
                        // it would be more performant to just call it directly as there is additional overhead
                        // with SK searching the plugin collection.
                        Console.WriteLine("Intent: databasecustomer");

                        dbSchema = Util.GetDatabaseSchema();
                        // TODO: utilize NL2SQL to get only tables related to customers
                        jsonSchema = Util.GetDatabaseJsonSchema(false);

                        break;
                    }
                case "not_found":
                    {
                        Console.WriteLine("Intent: not_found");
                        break;
                    }
            }

            if (databaseIntent)
            {
                var systemPrompt = $@"You are responsible for generating and executing a SQL query in response to user input.
                                    Only target the tables described in the given database schema.

                                    1. Generate a query that is always entirely based on the targeted database schema.
                                    2. Execute the query using the available plugin.
                                    3. Summarize the results to the user.

                                    The database schema is described according to the following json schema:
                                    {jsonSchema}

                                    The targeted database schema is described by the following json:
                                    {dbSchema}
                                    ";

                _chatHistory.AddSystemMessage(systemPrompt);
                _chatHistory.AddUserMessage(chatRequest.prompt);
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

                if (renderImageWithResponse)
                {
                    Console.WriteLine("Use Assistant SDK to generate image from result");
                }                
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