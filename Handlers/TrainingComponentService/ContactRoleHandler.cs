using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for TrainingComponentContactRole operations - fetching and saving to database
    /// </summary>
    public static class ContactRoleHandler
    {
        /// <summary>
        /// Fetches all contact roles from TGA service, displays them, and saves to Supabase
        /// </summary>
        public static async Task<TrainingComponentContactRole[]> ProcessContactRoles(
            ContactRoleService contactRoleService,
            SupabaseService supabaseService)
        {
            Console.WriteLine("=== Getting Contact Roles ===");
            var contactRoles = contactRoleService.GetContactRoles();

            Console.WriteLine(" Count of Contact Roles:" + contactRoles.Length);
            if (contactRoles != null && contactRoles.Length > 0)
            {
                foreach (var role in contactRoles)
                {
                    Console.WriteLine($"Role: {role.Role}");
                    Console.WriteLine($"Description: {role.Description}");
                    Console.WriteLine($"AllowGroupContact: {role.AllowGroupContact}");
                    Console.WriteLine($"AllowMultipleCurrent: {role.AllowMultipleCurrent}");
                    Console.WriteLine($"IsImplicit: {role.IsImplicit}");
                    Console.WriteLine($"RequiredTrainingComponentTypes: {role.RequiredTrainingComponentTypes}");
                    Console.WriteLine();
                }

                Console.WriteLine("=== Saving Contact Roles to Supabase ===");
                try
                {
                    var saveStopwatch = Stopwatch.StartNew();
                    await supabaseService.SaveToSupabase(contactRoles, "contact_roles");
                    saveStopwatch.Stop();

                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n✓ Successfully saved {contactRoles.Length} Contact Roles to Supabase!");
                    Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                    Console.ForegroundColor = originalColor;
                }
                catch (Exception supabaseEx)
                {
                    Console.WriteLine($"ERROR: Failed to save to Supabase: {supabaseEx.Message}");
                    if (supabaseEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {supabaseEx.InnerException.Message}");
                    }
                    Console.WriteLine("Continuing with rest of the application...\n");
                }

                return contactRoles;
            }

            Console.WriteLine("No contact roles found.\n");
            return null;
        }
    }
}
