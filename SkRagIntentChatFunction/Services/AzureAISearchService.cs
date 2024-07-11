namespace SkRagIntentChatFunction.Services
{
    using Azure.Search.Documents;
    using Azure;
    using Azure.Search.Documents.Indexes;
    using Azure.Search.Documents.Models;
    using SkRagIntentChatFunction.Interfaces;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.AI.OpenAI;

    internal class AzureAISearchService : IAzureAISearchService
    {
        private readonly List<string> _defaultVectorFields = new() { "contentVector" };

        private readonly SearchIndexClient _indexClient;

        // not being used right now
        private const string _modelName = "text-embedding-ada-002";
        private const int _ModelDimensions = 1536;
        private const string _SemanticSearchConfigName = "my-semantic-config";

        public AzureAISearchService(SearchIndexClient indexClient)
        {
            this._indexClient = indexClient;
        }

        public async Task<string?> SearchAsync(string collectionName, ReadOnlyMemory<float> vector, List<string>? searchFields = null, CancellationToken cancellationToken = default)
        {
            // Get client for search operations
            SearchClient searchClient = this._indexClient.GetSearchClient(collectionName);

            // Use search fields passed from Plugin or default fields configured in this class.
            List<string> fields = searchFields is { Count: > 0 } ? searchFields : this._defaultVectorFields;

            // Configure request parameters
            VectorizedQuery vectorQuery = new(vector);
            fields.ForEach(field => vectorQuery.Fields.Add(field));

            SearchOptions searchOptions = new() { VectorSearch = new() { Queries = { vectorQuery } } };

            // Perform search request
            Response<SearchResults<IndexSchema>> response = await searchClient.SearchAsync<IndexSchema>(searchOptions, cancellationToken);

            List<IndexSchema> results = new();

            // Collect search results
            await foreach (SearchResult<IndexSchema> result in response.Value.GetResultsAsync())
            {
                results.Add(result.Document);
            }

            // Return text from first result.
            // In real applications, the logic can check document score, sort and return top N results
            // or aggregate all results in one text.
            // The logic and decision which text data to return should be based on business scenario. 
            return results.FirstOrDefault()?.Chunk;
        }

        public async Task<string> SemanticHybridSearchAsync(ReadOnlyMemory<float> embedding, string query, int k = 3)
        {
            StringBuilder content = new StringBuilder();

            try
            {

                // Get client for search operations
                SearchClient searchClient = this._indexClient.GetSearchClient("obvector-1709061059373");

                var searchOptions = new SearchOptions
                {
                    VectorSearch = new()
                    {
                        Queries = { new VectorizedQuery(embedding.ToArray()) { KNearestNeighborsCount = 3, Fields = { "vector" } } }
                    },
                    SemanticSearch = new()
                    {
                        SemanticConfigurationName = "obvector-1709061059373-semantic-configuration",
                        QueryCaption = new(QueryCaptionType.Extractive),
                        QueryAnswer = new(QueryAnswerType.Extractive),
                    },
                    QueryType = SearchQueryType.Semantic,
                    Select = { "title", "chunk", },

                };

                SearchResults<SearchDocument> response = await searchClient.SearchAsync<SearchDocument>(query, searchOptions);

                int count = 0;
                Console.WriteLine($"Semantic Hybrid Search Results:");

                Console.WriteLine($"\nQuery Answer:");
                foreach (QueryAnswerResult result in response.SemanticSearch.Answers)
                {
                    Console.WriteLine($"Answer Highlights: {result.Highlights}");
                    Console.WriteLine($"Answer Text: {result.Text}");
                }

                await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
                {
                    count++;
                    Console.WriteLine($"Title: {result.Document["title"]}");
                    Console.WriteLine($"Reranker Score: {result.SemanticSearch.RerankerScore}");
                    Console.WriteLine($"Score: {result.Score}");
                    Console.WriteLine($"Content: {result.Document["chunk"]}");
                    content.AppendLine(result.Document.ToString());

                    if (result.SemanticSearch.Captions != null)
                    {
                        var caption = result.SemanticSearch.Captions.FirstOrDefault();
                        if (caption != null)
                        {
                            if (!string.IsNullOrEmpty(caption.Highlights))
                            {
                                Console.WriteLine($"Caption Highlights: {caption.Highlights}\n");
                            }
                            else
                            {
                                Console.WriteLine($"Caption Text: {caption.Text}\n");
                            }
                        }
                    }
                }
                Console.WriteLine($"Total Results: {count}");
            }
            catch (NullReferenceException)
            {
                Console.WriteLine("Total Results: 0");
            }
            return content.ToString();
        }

        public async Task<string> SimpleHybridSearchAsync(ReadOnlyMemory<float> embedding, string query, int k = 3)
        {
            StringBuilder content = new StringBuilder();

            // Get client for search operations
            SearchClient searchClient = this._indexClient.GetSearchClient("obvector-1709061059373");


            // Perform the vector similarity search  
            var searchOptions = new SearchOptions
            {
                VectorSearch = new()
                {
                    Queries = { new VectorizedQuery(embedding.ToArray()) { KNearestNeighborsCount = k, Fields = { "vector" } } }
                },
                Size = k,
                Select = { "title", "chunk" },
            };

            SearchResults<SearchDocument> response = await searchClient.SearchAsync<SearchDocument>(query, searchOptions);

            int count = 0;
            await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
            {
                count++;
                Console.WriteLine($"Title: {result.Document["title"]}");
                Console.WriteLine($"Score: {result.Score}\n");
                Console.WriteLine($"Content: {result.Document["chunk"]}");
                content.AppendLine(result.Document["chunk"].ToString() ?? string.Empty);
            }
            Console.WriteLine($"Total Results: {count}");

            return content.ToString();
        }

        // Function to generate embeddings  
        private static async Task<IReadOnlyList<float>> GenerateEmbeddings(string text, OpenAIClient openAIClient)
        {
            EmbeddingsOptions embeddingsOptions = new()
            {
                DeploymentName = _modelName,
                Input = { text },
            };
            var response = await openAIClient.GetEmbeddingsAsync(embeddingsOptions);

            float[] embeddingArray = response.Value.Data[0].Embedding.ToArray();

            List<float> embeddingList = new List<float>(embeddingArray);

            return embeddingList;
        }
    }

    class IndexSchema
    {
        [JsonPropertyName("chunk_id")]
        public string ChunkId { get; set; } = string.Empty;

        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; } = string.Empty;

        [JsonPropertyName("chunk")]
        public string Chunk { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("contentVector")]
        public ReadOnlyMemory<float> ContentVector { get; set; }
    }
}
