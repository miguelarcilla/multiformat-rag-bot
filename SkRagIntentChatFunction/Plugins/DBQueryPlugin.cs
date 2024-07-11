namespace SkRagIntentChatFunction.Plugins
{
    using Microsoft.SemanticKernel;
    using System.ComponentModel;

    public class DBQueryPlugin
    {
        private static bool _hrToggleContact;

        [KernelFunction]
        [Description("Allows a user find who their HR contact is based on the location that they pass in.  If Location is not provided then set it to null")]
        public static string GetHRContact([Description("Return who the HR Contact is for the user")] string query, string location)
        {
            if (location == null)
            {
                return "Please provide the location information?";
            }

            if (_hrToggleContact)
            {
                _hrToggleContact = false;
                return "Your HR contact is Steve Smith";
            }
            else
            {
                _hrToggleContact = true;
                return "Your HR contact is Lisa Jones";
            }

            return "Test Response";
        }
    }
}