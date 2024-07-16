using Azure.AI.OpenAI.Assistants;
using Azure;
using SkRagIntentChatFunction.Interfaces;

namespace SkRagIntentChatFunction.Services
{
    public class AzureAIAssistantService : IAzureAIAssistantService
    {
        private string _endPoint;
        private string _key;
        private string _deployedModel;
        private AssistantsClient _assistantsClient;

        public AzureAIAssistantService(string endPoint, string apiKey, string deploymentModel)
        {
            _endPoint = endPoint;
            _key = apiKey;
            _deployedModel = deploymentModel;
            _assistantsClient = new AssistantsClient(new Uri(_endPoint), new AzureKeyCredential(_key));
        }

        public async Task<(string assistantId, byte[] fileBytes)> RunAssistantAsync(string assistantName, string instructions, string prompt)
        {
            Assistant assistant = await _assistantsClient.CreateAssistantAsync(

            new AssistantCreationOptions(_deployedModel)
            {
                Name = assistantName,
                Instructions = instructions,
                Tools = { new CodeInterpreterToolDefinition() },
            });

            AssistantThread thread = await _assistantsClient.CreateThreadAsync();

            while (true)
            {
                Console.WriteLine($"User > {prompt}");

                // Add a user question to the thread
                ThreadMessage message = await _assistantsClient.CreateMessageAsync(
                    thread.Id,
                    MessageRole.User,
                    prompt);

                // Run the thread
                ThreadRun run = await _assistantsClient.CreateRunAsync(
                    thread.Id,
                    new CreateRunOptions(assistant.Id)
                );

                // Wait for the assistant to respond
                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    run = await _assistantsClient.GetRunAsync(thread.Id, run.Id);
                }

                while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

                if (run.Status == RunStatus.Completed)
                {
                    // Get the messages
                    PageableList<ThreadMessage> messagesPage = await _assistantsClient.GetMessagesAsync(thread.Id);
                    IReadOnlyList<ThreadMessage> messages = messagesPage.Data;

                    var ts = messagesPage.Data.FirstOrDefault(x => x.FileIds.Count > 0);
                    var fileId = ts.FileIds.FirstOrDefault();
                    Response<BinaryData> content = await _assistantsClient.GetFileContentAsync(fileId);

                    // Convert the binary data to a byte array
                    byte[] data = content.Value.ToArray();
                    return (assistant.Id, data);
                }
            }
        }

        public async Task DeleteAssistantAsync(string assistantId)
        {
            await _assistantsClient.DeleteAssistantAsync(assistantId);
        }
    }
}
