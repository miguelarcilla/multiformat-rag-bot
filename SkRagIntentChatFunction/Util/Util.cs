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
                    - databasewithimage: Use this intent to answer questions about product queries when an image is expected to depict the data.
                    - databasewithoutimage: Use this intent to answer questions about product queries with text only.
                    - manual: Use this intent to answer questions about the Tesla manual.
                    - not_found: Use this intent if you cannot find a suitable answer

                [Examples for database type questions without images]
                User question: How many bikes are there?
                Intent: database
                User question: Is the color red more popular than blue?
                Intent: database
                User question: Retrieve the number of bikes in the warehouse.
                Intent: database

                [Examples for database type questions with images]
                User question: How many bikes are there? Plot a bar chart of the number of bikes versus other products.
                Intent: database
                User question: How many red bikes are there? Plot a pie chart of different colors of bikes.
                Intent: database
                User question: Retrieve the number of products over $100. Plot a bar chart of the cost distribution of all products.
                Intent: database

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

                // use regex and Linq to find the intent that is repeated the most
                var words = Regex.Split(intentResult.ToLower(), @"\W+")
                    .Where(w => w.Length >= 3)
                    .GroupBy(w => w)
                    .OrderByDescending(g => g.Count())
                    .First();

                intent = words.Key;
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

        public static string GetDatabaseSchema()
        {
            var databaseSchema =
                """
                  {
                  "Name": "AdventureWorksLT",
                  "Platform": "Microsoft SQL Server",
                  "Description": "Product, sales, and customer data for the AdentureWorks company.",
                  "Tables": [
                    {
                      "Name": "SalesLT.Address",
                      "Columns": [
                        {
                          "Name": "AddressID",
                          "Type": "int",
                          "IsPrimary": true
                        },
                        {
                          "Name": "AddressLine1",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "AddressLine2",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "City",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "CountryRegion",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "PostalCode",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        },
                        {
                          "Name": "StateProvince",
                          "Type": "nvarchar"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.Customer",
                      "Columns": [
                        {
                          "Name": "CustomerID",
                          "Type": "int",
                          "IsPrimary": true
                        },
                        {
                          "Name": "CompanyName",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "EmailAddress",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "FirstName",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "LastName",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "MiddleName",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "NameStyle",
                          "Type": "bit"
                        },
                        {
                          "Name": "Phone",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        },
                        {
                          "Name": "SalesPerson",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Suffix",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Title",
                          "Type": "nvarchar"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.CustomerAddress",
                      "Columns": [
                        {
                          "Name": "AddressID",
                          "Type": "int",
                          "IsPrimary": true,
                          "ReferencedTable": "SalesLT.Address",
                          "ReferencedColumn": "AddressID"
                        },
                        {
                          "Name": "CustomerID",
                          "Type": "int",
                          "IsPrimary": true,
                          "ReferencedTable": "SalesLT.Customer",
                          "ReferencedColumn": "CustomerID"
                        },
                        {
                          "Name": "AddressType",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.Product",
                      "Columns": [
                        {
                          "Name": "ProductID",
                          "Type": "int",
                          "IsPrimary": true
                        },
                        {
                          "Name": "Color",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "DiscontinuedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "ListPrice",
                          "Type": "money"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "Name",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ProductCategoryID",
                          "Type": "int",
                          "ReferencedTable": "SalesLT.ProductCategory",
                          "ReferencedColumn": "ProductCategoryID"
                        },
                        {
                          "Name": "ProductModelID",
                          "Type": "int",
                          "ReferencedTable": "SalesLT.ProductModel",
                          "ReferencedColumn": "ProductModelID"
                        },
                        {
                          "Name": "ProductNumber",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        },
                        {
                          "Name": "SellEndDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "SellStartDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "Size",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "StandardCost",
                          "Type": "money"
                        },
                        {
                          "Name": "ThumbNailPhoto",
                          "Type": "varbinary"
                        },
                        {
                          "Name": "ThumbnailPhotoFileName",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Weight",
                          "Type": "decimal"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.ProductCategory",
                      "Columns": [
                        {
                          "Name": "ProductCategoryID",
                          "Type": "int",
                          "IsPrimary": true
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "Name",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ParentProductCategoryID",
                          "Type": "int",
                          "ReferencedTable": "SalesLT.ProductCategory",
                          "ReferencedColumn": "ProductCategoryID"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.ProductDescription",
                      "Columns": [
                        {
                          "Name": "ProductDescriptionID",
                          "Type": "int",
                          "IsPrimary": true
                        },
                        {
                          "Name": "Description",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.ProductModel",
                      "Columns": [
                        {
                          "Name": "ProductModelID",
                          "Type": "int",
                          "IsPrimary": true
                        },
                        {
                          "Name": "CatalogDescription",
                          "Type": "xml"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "Name",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.ProductModelProductDescription",
                      "Columns": [
                        {
                          "Name": "Culture",
                          "Type": "nchar",
                          "IsPrimary": true
                        },
                        {
                          "Name": "ProductDescriptionID",
                          "Type": "int",
                          "IsPrimary": true,
                          "ReferencedTable": "SalesLT.ProductDescription",
                          "ReferencedColumn": "ProductDescriptionID"
                        },
                        {
                          "Name": "ProductModelID",
                          "Type": "int",
                          "IsPrimary": true,
                          "ReferencedTable": "SalesLT.ProductModel",
                          "ReferencedColumn": "ProductModelID"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.SalesOrderDetail",
                      "Columns": [
                        {
                          "Name": "SalesOrderDetailID",
                          "Type": "int",
                          "IsPrimary": true
                        },
                        {
                          "Name": "SalesOrderID",
                          "Type": "int",
                          "IsPrimary": true,
                          "ReferencedTable": "SalesLT.SalesOrderHeader",
                          "ReferencedColumn": "SalesOrderID"
                        },
                        {
                          "Name": "LineTotal",
                          "Type": "numeric"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "OrderQty",
                          "Type": "smallint"
                        },
                        {
                          "Name": "ProductID",
                          "Type": "int",
                          "ReferencedTable": "SalesLT.Product",
                          "ReferencedColumn": "ProductID"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        },
                        {
                          "Name": "UnitPrice",
                          "Type": "money"
                        },
                        {
                          "Name": "UnitPriceDiscount",
                          "Type": "money"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.SalesOrderHeader",
                      "Columns": [
                        {
                          "Name": "SalesOrderID",
                          "Type": "int",
                          "IsPrimary": true
                        },
                        {
                          "Name": "AccountNumber",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "BillToAddressID",
                          "Type": "int",
                          "ReferencedTable": "SalesLT.Address",
                          "ReferencedColumn": "AddressID"
                        },
                        {
                          "Name": "Comment",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "CreditCardApprovalCode",
                          "Type": "varchar"
                        },
                        {
                          "Name": "CustomerID",
                          "Type": "int",
                          "ReferencedTable": "SalesLT.Customer",
                          "ReferencedColumn": "CustomerID"
                        },
                        {
                          "Name": "DueDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "Freight",
                          "Type": "money"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "OnlineOrderFlag",
                          "Type": "bit"
                        },
                        {
                          "Name": "OrderDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "PurchaseOrderNumber",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "RevisionNumber",
                          "Type": "tinyint"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        },
                        {
                          "Name": "SalesOrderNumber",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ShipDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "ShipMethod",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ShipToAddressID",
                          "Type": "int",
                          "ReferencedTable": "SalesLT.Address",
                          "ReferencedColumn": "AddressID"
                        },
                        {
                          "Name": "Status",
                          "Type": "tinyint"
                        },
                        {
                          "Name": "SubTotal",
                          "Type": "money"
                        },
                        {
                          "Name": "TaxAmt",
                          "Type": "money"
                        },
                        {
                          "Name": "TotalDue",
                          "Type": "money"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.vGetAllCategories",
                      "IsView": true,
                      "Columns": [
                        {
                          "Name": "ParentProductCategoryName",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ProductCategoryID",
                          "Type": "int"
                        },
                        {
                          "Name": "ProductCategoryName",
                          "Type": "nvarchar"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.vProductAndDescription",
                      "IsView": true,
                      "Columns": [
                        {
                          "Name": "Culture",
                          "Type": "nchar"
                        },
                        {
                          "Name": "Description",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Name",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ProductID",
                          "Type": "int"
                        },
                        {
                          "Name": "ProductModel",
                          "Type": "nvarchar"
                        }
                      ]
                    },
                    {
                      "Name": "SalesLT.vProductModelCatalogDescription",
                      "IsView": true,
                      "Columns": [
                        {
                          "Name": "BikeFrame",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Color",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Copyright",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Crankset",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "MaintenanceDescription",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Manufacturer",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Material",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ModifiedDate",
                          "Type": "datetime"
                        },
                        {
                          "Name": "Name",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "NoOfYears",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Pedal",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "PictureAngle",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "PictureSize",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ProductLine",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ProductModelID",
                          "Type": "int"
                        },
                        {
                          "Name": "ProductPhotoID",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "ProductURL",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "RiderExperience",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "rowguid",
                          "Type": "uniqueidentifier"
                        },
                        {
                          "Name": "Saddle",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Style",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Summary",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "WarrantyDescription",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "WarrantyPeriod",
                          "Type": "nvarchar"
                        },
                        {
                          "Name": "Wheel",
                          "Type": "nvarchar"
                        }
                      ]
                    }
                  ]
                }
                """;

            return databaseSchema;
        }
    }
}