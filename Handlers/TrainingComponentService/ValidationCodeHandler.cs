using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for ValidationCode operations - fetching and saving to database
    /// </summary>
    public static class ValidationCodeHandler
    {
        /// <summary>
        /// Fetches all ValidationCodes from TGA service, displays them, and saves to Supabase
        /// </summary>
        public static async Task<ValidationCode[]> ProcessValidationCodes(
            ValidationCodeService validationCodeService,
            SupabaseService supabaseService)
        {
            Console.WriteLine("=== Getting Validation Codes ===");
            var validationCodes = validationCodeService.GetValidationCodes();

            Console.WriteLine(" Count of Validation Codes:" + validationCodes.Length);
            if (validationCodes != null && validationCodes.Length > 0)
            {
                foreach (var code in validationCodes)
                {
                    Console.WriteLine($"Code: {code.Code}");
                    Console.WriteLine($"SubCode: {code.SubCode}");
                    Console.WriteLine($"Message: {code.Message}");
                    Console.WriteLine();
                }

                Console.WriteLine("=== Saving Validation Codes to Supabase ===");
                try
                {
                    var saveStopwatch = Stopwatch.StartNew();
                    await supabaseService.SaveToSupabase(validationCodes, "validation_codes");
                    saveStopwatch.Stop();

                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n✓ Successfully saved {validationCodes.Length} Validation Codes to Supabase!");
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

                return validationCodes;
            }

            Console.WriteLine("No validation codes found.\n");
            return null;
        }
    }
}
