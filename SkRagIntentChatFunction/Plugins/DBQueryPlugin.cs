namespace SkRagIntentChatFunction.Plugins
{
    using Microsoft.SemanticKernel;
    using System.ComponentModel;

    public class DBQueryPlugin
    {
        private static bool _hrToggleContact;

        [KernelFunction]
        [Description("Executes a SQL query to provide information about a product.")]
        public static string GetProductDetails(string query)
        {
            Console.WriteLine(query);

            string results = "";

            return results;
            /*if (_hrToggleContact)
            {
                _hrToggleContact = false;
                return "Your HR contact is Steve Smith";
            }
            else
            {
                _hrToggleContact = true;
                return "Your HR contact is Lisa Jones";
            }

            return "Test Response";*/
        }
    }
}