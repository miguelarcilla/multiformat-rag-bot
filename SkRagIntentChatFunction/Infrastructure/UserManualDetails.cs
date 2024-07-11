namespace SkRagIntentChatFunction.Infrastructure
{
    internal class UserManualDetails
    {
        public string ChunkId { get; set; }

        public string Chunk{ get; set; }

        public string Title { get; set; }

        public double Score { get; set; }

        public double RerankerScore { get; set; }
    }
}