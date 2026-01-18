using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TgaGateway2.Models;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for Usage Recommendation operations - fetching and saving to database
    /// </summary>
    public static class UsageRecommendationHandler
    {
        /// <summary>
        /// Fetches usage recommendations via GetDetails for each training component code
        /// </summary>
        public static async Task<List<UsageRecommendationRecord>> ProcessUsageRecommendations(
            TrainingComponentSummaryService summaryService,
            UsageRecommendationService usageRecommendationService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Usage Recommendations ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allUsageRecommendations = new List<UsageRecommendationRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalUsageRecommendationsSaved = 0;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageByKey = new Dictionary<string, UsageRecommendationRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var usageRecommendations = usageRecommendationService.GetUsageRecommendations(summary.Code);
                            foreach (var usageRecommendation in usageRecommendations)
                            {
                                var record = new UsageRecommendationRecord
                                {
                                    UsageRecommendationKey = BuildUsageRecommendationKey(summary.Code, usageRecommendation),
                                    TrainingComponentCode = summary.Code,
                                    State = usageRecommendation.State,
                                    ActionOnEntity = usageRecommendation.ActionOnEntity.ToString(),
                                    StartDate = usageRecommendation.StartDate,
                                    EndDate = usageRecommendation.EndDate
                                };

                                // De-duplicate within the same batch
                                pageByKey[record.UsageRecommendationKey] = record;
                            }
                        }

                        var pageUsageRecommendations = pageByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageUsageRecommendations.Count} usage recommendations) to Supabase...");
                        if (pageUsageRecommendations.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageUsageRecommendations.ToArray(), "usage_recommendations");
                        }
                        totalUsageRecommendationsSaved += pageUsageRecommendations.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total usage recommendations saved: {totalUsageRecommendationsSaved})");
                        Console.WriteLine();

                        allUsageRecommendations.AddRange(pageUsageRecommendations);
                    },
                    startDate,
                    endDate,
                    maxResults,
                    pageSize: 200);

                if (totalProcessed == 0)
                {
                    Console.WriteLine("No training component summaries found.\n");
                    return null;
                }

                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully processed {totalProcessed} components.");
                Console.WriteLine($"✓ Saved {allUsageRecommendations.Count} usage recommendations to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allUsageRecommendations;
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
                Console.WriteLine($"\nNote: {allUsageRecommendations.Count} usage recommendations were saved before the error occurred.\n");
                return allUsageRecommendations.Count > 0 ? allUsageRecommendations : null;
            }
        }

        private static string BuildUsageRecommendationKey(string trainingComponentCode, UsageRecommendation usageRecommendation)
        {
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                usageRecommendation.State ?? string.Empty,
                usageRecommendation.StartDate ?? string.Empty,
                usageRecommendation.EndDate ?? string.Empty,
                usageRecommendation.ActionOnEntity.ToString()
            });

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
