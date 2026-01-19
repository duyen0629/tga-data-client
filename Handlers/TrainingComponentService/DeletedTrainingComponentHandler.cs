using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for deleted training components - fetching and saving to database
    /// </summary>
    public static class DeletedTrainingComponentHandler
    {
        /// <summary>
        /// Fetches deleted training components and saves them to Supabase
        /// </summary>
        public static async Task<List<DeletedTrainingComponent>> ProcessDeletedTrainingComponents(
            DeletedTrainingComponentService deletedService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0,
            int pageSize = 500)
        {
            Console.WriteLine("=== Getting and Saving Deleted Training Components ===");

            var allDeleted = new List<DeletedTrainingComponent>();
            var saveStopwatch = Stopwatch.StartNew();

            try
            {
                int totalProcessed = await deletedService.SearchDeletedByDeletedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        Console.WriteLine($"  Saving Page {pageNumber} ({pageResults.Length} deleted components) to Supabase...");
                        if (pageResults.Length > 0)
                        {
                            await supabaseService.SaveToSupabase(pageResults, "deleted_training_components");
                        }
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total deleted saved: {totalSoFar + pageResults.Length})");
                        Console.WriteLine();

                        allDeleted.AddRange(pageResults);
                    },
                    startDate,
                    endDate,
                    maxResults,
                    pageSize);

                if (totalProcessed == 0)
                {
                    Console.WriteLine("No deleted training components found.\n");
                    return null;
                }

                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully processed and saved {totalProcessed} deleted training components to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allDeleted;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ ERROR: Failed during processing!");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Exception Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.WriteLine($"\nNote: {allDeleted.Count} records were successfully saved before the error occurred.\n");
                return allDeleted.Count > 0 ? allDeleted : null;
            }
        }
    }
}
