using SkRagIntentChatFunction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkRagIntentChatFunction.Interfaces
{
    public interface IAzureCosmosDbService
    {
        Task<Session> InsertSessionAsync(Session session);
        Task<Message> InsertMessageAsync(Message message);
        Task<List<Session>> GetSessionsAsync();
        Task<List<Message>> GetSessionMessagesAsync(string sessionId);
        Task<Session> UpdateSessionAsync(Session session);
        Task<bool> SessionExists(string sessionId);
        Task<Session> GetSessionAsync(string sessionId);
        Task UpsertSessionBatchAsync(params dynamic[] messages);
        Task DeleteSessionAndMessagesAsync(string sessionId);
    }
}
