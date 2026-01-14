using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for TrainingComponentSummary operations - fetching training component summaries and saving to database
    /// </summary>
    public static class TrainingComponentSummaryHandler
    {
        /// <summary>
        /// Fetches training component summaries and saves them to Supabase
        /// </summary>
        /// <param name="summaryService">Training component summary service instance</param>
        /// <param name="supabaseService">Supabase service instance</param>
        /// <param name="startDate">Start date for search</param>
        /// <param name="endDate">End date for search</param>
        /// <param name="maxResults">Maximum number of results to return (0 = try to get all via pagination)</param>
        /// <returns>List of TrainingComponentSummary objects (or null if none found)</returns>
        public static async Task<List<TrainingComponentSummary>> ProcessTrainingComponentSummaries(
            TrainingComponentSummaryService summaryService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Training Component Summaries ===");

            var allSummaries = new List<TrainingComponentSummary>();
            var saveStopwatch = Stopwatch.StartNew();

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        // Save this page to Supabase immediately
                        Console.WriteLine($"  Saving Page {pageNumber} ({pageResults.Length} records) to Supabase...");
                        await supabaseService.SaveToSupabase(pageResults, "training_component_summaries");
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total saved so far: {totalSoFar + pageResults.Length})");
                        Console.WriteLine();

                        // Keep track of all summaries for return value
                        allSummaries.AddRange(pageResults);
                    },
                    startDate,
                    endDate,
                    maxResults);

                if (totalProcessed == 0)
                {
                    Console.WriteLine("No training component summaries found.\n");
                    return null;
                }
                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully processed and saved {totalProcessed} Training Component Summaries to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allSummaries;
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
                Console.WriteLine($"\nNote: {allSummaries.Count} records were successfully saved before the error occurred.\n");
                return allSummaries.Count > 0 ? allSummaries : null;
            }
        }
    }
}
