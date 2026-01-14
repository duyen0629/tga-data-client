using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <param name="startDate">Start date for search (defaults to 10 years ago)</param>
        /// <param name="endDate">End date for search (defaults to now)</param>
        /// <param name="maxResults">Maximum number of results to return (0 = try to get all via pagination)</param>
        /// <returns>List of TrainingComponentSummary objects (or null if none found)</returns>
        public static async Task<List<TrainingComponentSummary>> ProcessTrainingComponentSummaries(
            TrainingComponentSummaryService summaryService,
            SupabaseService supabaseService,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting Training Component Summaries ===");

            var summaries = summaryService.SearchByModifiedDate(startDate, endDate, maxResults);

            if (summaries == null || summaries.Count == 0)
            {
                Console.WriteLine("No training component summaries found.\n");
                return null;
            }

            Console.WriteLine($"\nFound {summaries.Count} training component summaries.");

            // Display first few summaries as sample
            Console.WriteLine("\n--- Sample Training Component Summaries (first 10) ---");
            foreach (var summary in summaries.Take(10))
            {
                Console.WriteLine($"Code: {summary.Code}");
                Console.WriteLine($"Title: {summary.Title ?? "N/A"}");
                Console.WriteLine($"Component Type: {summary.ComponentType}");
                Console.WriteLine($"Is Current: {summary.IsCurrent?.ToString() ?? "N/A"}");
                Console.WriteLine();
            }

            if (summaries.Count > 10)
            {
                Console.WriteLine($"... and {summaries.Count - 10} more summaries.\n");
            }

            // Save to Supabase
            Console.WriteLine("=== Saving Training Component Summaries to Supabase ===");
            Console.WriteLine($"Attempting to save {summaries.Count} training component summaries to table 'training_component_summaries'...");
            try
            {
                await supabaseService.SaveToSupabase(summaries.ToArray(), "training_component_summaries");
                Console.WriteLine($"✓ Successfully saved {summaries.Count} Training Component Summaries to Supabase!\n");
            }
            catch (Exception supabaseEx)
            {
                Console.WriteLine($"✗ ERROR: Failed to save to Supabase!");
                Console.WriteLine($"Exception Type: {supabaseEx.GetType().Name}");
                Console.WriteLine($"Exception Message: {supabaseEx.Message}");
                if (supabaseEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {supabaseEx.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {supabaseEx.StackTrace}");
                Console.WriteLine("Continuing with rest of the application...\n");
            }

            return summaries;
        }
    }
}
