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
                    - database: Use this intent to answer questions about HR queries.
                    - manual: Use this intent to answer questions about the Tesla manual.
                    - not_found: Use this intent if you cannot find a suitable answer

                [Examples for database type questions]
                User question: Who is my HR representative?
                Intent: database
                User question: Retrieve my current level
                Intent: database
                User question: Retrieve my current salary
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
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return intent;
        }

        public static string GetDatabasePrompt(bool isAdmin)
        {
            var columns = @"positioncode:Unique identifier for position records. Users will refer to this as “Position ID.” For example, if a user asks “What is Tom Smith’s Position ID?” they are referring to this field,
                                country: Country of the position, 
                                jobtitle: Job Title of a position associated with a PositionCode. Refers to the specific role or position title that an individual holds within the organization,
                                function:Position business function, for example - Marketing, Engineering, etc.,
                                fte: Indicates whether an employee is full-time or part-time. FTP of 1 means Full Time. FTP less than 1 means part time,
                                fullname: Name of the person associated with an EmployeeCode., 
                                pcr:Position Count Rollup. Total number of In Org, Non-excluded positions that sit under a manager including the manager itself. When a user asks “How many positions are in Tom Smith’s organization,” they are looking for PCR,
                                layer:This holds an integer value that indicates a position’s or employee’s relative distance from the CEO. For example, if the CEO is in Layer 1, then the CEO’s direct reports are in Layer 2.,
                                soc:Span of control. The number of In Org, non-excluded positions that report directly to a manager,
                                ismanager: TRUE identifies positions with direct reports,
                                empfunction:, 
                                ftp: Indicates whether a position is full - time or part - time.FTP of 1 means Full Time.FTP less than 1 means part time.,
                                empjobtitle:,
                                isexcludedfromanalysis: Indicates that a position is not counted in a manager’s SOC or PCR.,
                                emplayer:,
                                employeecode: Unique identifier for employee records. Users will refer to this as “Employee ID.” For example, if a user asks “What is Tom Smith’s Employee ID ?” they are referring to this field.
                                empcountry: Country of the employee.,
                                layersbelow: Indicates the total number of layers below a position,
                                trc:Total Record Count.Total number of records that sit under a manager,
                                level: used to indicate a position’s pay grade or rank within the organization,
                                isic:TRUE indicates a position with no direct reports.,
                                supervisorcode: PositionCode of the position that another position or employee reports to.Users will refer to this as “Supervisor ID.” For example, if a user asks “What is Tom Smith’s Supervisor ID ?” they are referring to this field.,
                                status: Positions that are closed will have a status of 'Closed Positions'. 'In Org' indicates a position that is part of the organization and is not closed. Positions with statuses other than “In Org” should not be counted when asked about Totals, Medians, or Averages.For example, if a user asks “What is the average cost of my Marketing function ?” only analyze positions with the status “In Org”.,
                                isnewposition:When TRUE, Identifies new positions that did not exist in the baseline,
                                issupervisorchanged:When TRUE, identifies positions whose supervisor has changed since the baseline,";

            if (isAdmin) columns += @"
                                compensation: Cost or salary of a position. Use this field when users ask about total or average cost, or when they ask about a position’s or employee’s salary.,
                                positionid:This is a database field that captures the unique identifier for position records. It has no meaning to users.Do not mention this field when answering a user query.If a user asks about Position ID, they are referring to PositionCode,
                                employeeid:This is a database field that captures the unique identifier for employee records. It has no meaning to users.Do not mention this field when answering a user query.If a user asks about Employee ID, they are referring to EmployeeCode,
                                uniquepositionrefid:This is a database field that has no meaning to users.Do not mention this field when answering a user query,
                                uniqueemployeerefid:This is a database field that has no meaning to users.Do not mention this field when answering a user query,
                                Layer 1:,
                                Layer 1 Name:,
                                Layer 2:,
                                Layer 2 Name:,
                                Layer 3:,
                                Layer 3 Name:,
                                Layer 4:,
                                Layer 4 Name:,
                                Layer 5:,
                                Layer 5 Name:,
                                Layer 6:,
                                Layer 6 Name:,
                                Layer 7:,
                                Layer 7 Name:,
                                Layer 8:,
                                Layer 8 Name:, ";
            return @$"The plugin return the following data for each position:
                                {columns}
                                Provide detailed explanations of any calculations.
                                Use only these columns to generate the query. Don't guess. If you need more information, ask the user for additional information.
                                User may be retricted to access other fields";

        }
    }
}