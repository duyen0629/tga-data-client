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
    /// Handler for Completion Mapping operations - fetching and saving to database
    /// </summary>
    public static class CompletionMappingHandler
    {
        /// <summary>
        /// Fetches completion mappings via GetDetails for each training component code
        /// </summary>
        public static async Task<List<CompletionMappingRecord>> ProcessCompletionMappings(
            TrainingComponentSummaryService summaryService,
            CompletionMappingService completionMappingService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Completion Mappings ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allCompletionMappings = new List<CompletionMappingRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalCompletionMappingsSaved = 0;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageByKey = new Dictionary<string, CompletionMappingRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var mappings = completionMappingService.GetCompletionMappings(summary.Code);
                            foreach (var mapping in mappings)
                            {
                                var record = new CompletionMappingRecord
                                {
                                    CompletionMappingKey = BuildCompletionMappingKey(summary.Code, mapping),
                                    TrainingComponentCode = summary.Code,
                                    Code = mapping.Code,
                                    IsMandatory = mapping.IsMandatory,
                                    ActionOnEntity = mapping.ActionOnEntity.ToString(),
                                    StartDate = mapping.StartDate,
                                    EndDate = mapping.EndDate
                                };

                                // De-duplicate within the same batch
                                pageByKey[record.CompletionMappingKey] = record;
                            }
                        }

                        var pageCompletionMappings = pageByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageCompletionMappings.Count} completion mappings) to Supabase...");
                        if (pageCompletionMappings.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageCompletionMappings.ToArray(), "completion_mappings");
                        }
                        totalCompletionMappingsSaved += pageCompletionMappings.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total completion mappings saved: {totalCompletionMappingsSaved})");
                        Console.WriteLine();

                        allCompletionMappings.AddRange(pageCompletionMappings);
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
                Console.WriteLine($"✓ Saved {allCompletionMappings.Count} completion mappings to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allCompletionMappings;
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
                Console.WriteLine($"\nNote: {allCompletionMappings.Count} completion mappings were saved before the error occurred.\n");
                return allCompletionMappings.Count > 0 ? allCompletionMappings : null;
            }
        }

        private static string BuildCompletionMappingKey(string trainingComponentCode, NrtCompletion mapping)
        {
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                mapping.Code ?? string.Empty,
                mapping.IsMandatory.ToString(),
                mapping.StartDate ?? string.Empty,
                mapping.EndDate ?? string.Empty,
                mapping.ActionOnEntity.ToString()
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
