using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for RecognitionManager operations - fetching and saving to database
    /// </summary>
    public static class RecognitionManagerHandler
    {
        /// <summary>
        /// Fetches all RecognitionManagers from TGA service, displays them, and saves to Supabase
        /// </summary>
        /// <param name="recognitionManagerService">Recognition manager service instance</param>
        /// <param name="supabaseService">Supabase service instance</param>
        /// <returns>Array of RecognitionManagers (or null if none found)</returns>
        public static async Task<RecognitionManager[]> ProcessRecognitionManagers(
            RecognitionManagerService recognitionManagerService,
            SupabaseService supabaseService)
        {
            Console.WriteLine("=== Getting Recognition Managers ===");
            var recognitionManagers = recognitionManagerService.GetRecognitionManagers();

            Console.WriteLine(" Count of Recognition Managers:" + recognitionManagers.Length);
            if (recognitionManagers != null && recognitionManagers.Length > 0)
            {
                foreach (var rm in recognitionManagers)
                {
                    Console.WriteLine($"Code: {rm.Code}");
                    Console.WriteLine($"Description: {rm.Description}");
                    Console.WriteLine($"ShortName: {rm.ShortName}");
                    Console.WriteLine();
                }

                // Save to Supabase
                Console.WriteLine("=== Saving Recognition Managers to Supabase ===");
                try
                {
                    var saveStopwatch = Stopwatch.StartNew();
                    await supabaseService.SaveToSupabase(recognitionManagers, "recognition_managers");
                    saveStopwatch.Stop();

                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n✓ Successfully saved {recognitionManagers.Length} Recognition Managers to Supabase!");
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

                return recognitionManagers;
            }
            else
            {
                Console.WriteLine("No recognition managers found.\n");
                return null;
            }
        }
    }
}
