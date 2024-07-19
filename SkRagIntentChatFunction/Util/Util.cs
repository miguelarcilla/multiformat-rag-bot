namespace SkRagIntentChatFunction
{
    using Microsoft.SemanticKernel.ChatCompletion;
    using Microsoft.SemanticKernel.Connectors.OpenAI;
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal static class Util
    {
        public static async Task<string> GetIntent(IChatCompletionService chat, string query)
        {
            // ChatHistory is local to this helper since we are only using it to detect intent
            ChatHistory chatHistory = new ChatHistory();

            var intent = "not_found";

            chatHistory.AddSystemMessage(
                $@"Return the intent of the user. The intent must be one of the following strings:
                    - databaseproduct: Use this intent to answer questions about product queries.
                    - databaseproduct-image: Use this intent to answer questions about product queries and include an image.
                    - databasecustomer: Use this intent to answer questions about customer queries.
                    - databasecustomer-image: Use this intent to answer questions about customer queries and include an image.
                    - manual: Use this intent to answer questions about the Tesla manual.
                    - not_found: Use this intent if you cannot find a suitable answer.
                Do NOT include the word Intent in the intent response; only respond with the intent strings above.

                [Examples for database product questions]
                User question: How many bikes are there?
                Intent: databaseproduct
                User question: Is the color red more popular than blue?
                Intent: databaseproduct
                User question: Retrieve the number of bikes in the warehouse.
                Intent: databaseproduct

                [Examples for database customer questions]
                User question: How many customers are registered?
                Intent: databasecustomer
                User question: How many customers are men?
                Intent: databasecustomer
                User question: How many customers are women?
                Intent: databasecustomer

                If the user asks for an image to describe the data, append the intent with -image. Examples of this
                are shown below.
                User question: How many bikes are there? Plot a bar chart of the number of bikes versus other products.
                Intent: databaseproduct-image
                User question: Plot the number of customers assigned to each salesperson in a bar chart.
                Intent: databasecustomer-image

                [Examples for manual type of questions]
                User question: What type of battery does the car use?
                Intent: manual
                User question: What are the major services required?
                Intent: manual
                User question: How do I change the cabin air filter?
                Intent: manual

                Per user query, what is the intent?
                Intent:");

            chatHistory.AddUserMessage(query);

            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                Temperature = .5,
                // This is very important as it allows us to instruct the model to give us 3 results for the prompt in one call, this is very powerful
                ResultsPerPrompt = 3,
            };

            try
            {
                // Call the chat completion asking for 3 rounds to attempt to identify the intent
                var result = await chat.GetChatMessageContentsAsync(
                    chatHistory,
                    executionSettings);

                string intentResult = string.Join(", ", result.Select(o => o.ToString()));

                // Matches words containing hyphens
                var wordFrequencies = Regex.Matches(intentResult.ToLower(), @"\b[\w-]+\b")
                                          .Cast<Match>()
                                          .Select(m => m.Value.ToLower())
                                          .GroupBy(s => s)
                                          .OrderByDescending(g => g.Count());

                intent = wordFrequencies.FirstOrDefault()?.Key;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return intent;
        }

        public static string GetDatabaseJsonSchema(bool isAdmin)
        {
            var jsonSchema = """
                  {
                  "$schema": "http://json-schema.org/draft-07/schema#",
                  "description": "Describes tables and views to be used querying a SQL database",
                  "type": "object",
                  "properties": {
                    "Name": {
                	  "description": "The database name",
                      "type": "string"
                    },
                    "Platform": {
                      "description": "The targeted SQL service platform",
                      "type": "string"
                    },
                    "Description": {
                      "description": "A functional decription of the database",
                      "type": "string"
                    },
                    "Tables": {
                      "description": "A list of table and view definitions",
                      "type": "array",
                      "items": {
                        "type": "object",
                        "properties": {
                          "Name": {
                            "description": "The name of the table or view",
                            "type": "string"
                          },
                          "Description": {
                            "description": "The description of the table",
                            "type": "string"
                          },
                          "Columns": {
                            "description": "A list of column definitions",
                            "type": "array",
                            "items": {
                              "type": "object",
                              "properties": {
                                "Name": {
                                  "description": "The name of the column",
                                  "type": "string"
                                },
                                "Description": {
                                  "description": "Describes the column",
                                  "type": "string"
                                },
                                "Type": {
                                  "description": "The type of the column",
                                  "type": "string"
                                },
                                "IsPrimary": {
                                  "description": "Indicates if the column is part of the primary key",
                                  "type": "boolean"
                                },
                                "ReferencedTable": {
                                  "description": "The name of the table referenced, if this column has a foreign key relationship to another table.",
                                  "type": "string"
                                },
                                "ReferencedColumn": {
                                  "description": "The name of the column referenced, if this column has a foreign key relationship to another table.",
                                  "type": "string"
                                }
                              },
                              "required": [
                                "Name",
                                "Type"
                              ]
                            }
                          },
                          "IsView": {
                            "description": "Indicates if the described table is actually a view",
                            "type": "boolean"
                          }
                        },
                        "required": [
                          "Name",
                          "Columns"
                        ]
                      }
                    }
                  },
                  "required": [
                    "Name",
                    "Platform",
                    "Tables"
                  ]
                }
                """;

            return jsonSchema;
        }
    }
}