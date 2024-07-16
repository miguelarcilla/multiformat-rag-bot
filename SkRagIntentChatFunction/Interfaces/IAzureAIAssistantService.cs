namespace SkRagIntentChatFunction.Interfaces
{
    public interface IAzureAIAssistantService
    {
        Task<(string assistantId, byte[] fileBytes)> RunAssistantAsync(string endPoint, string apiKey, string deploymentModel);
        Task DeleteAssistantAsync(string assistantId);
    }
}
