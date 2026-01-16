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
    /// Handler for Mapping operations - fetching and saving to database
    /// </summary>
    public static class MappingHandler
    {
        /// <summary>
        /// Fetches mapping information via GetDetails for each training component code
        /// </summary>
        public static async Task<List<MappingRecord>> ProcessMappings(
            TrainingComponentSummaryService summaryService,
            MappingService mappingService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Mapping Information ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allMappings = new List<MappingRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalMappingsSaved = 0;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageByKey = new Dictionary<string, MappingRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var mappings = mappingService.GetMappings(summary.Code);
                            foreach (var mapping in mappings)
                            {
                                var record = new MappingRecord
                                {
                                    MappingKey = BuildMappingKey(summary.Code, mapping),
                                    TrainingComponentCode = summary.Code,
                                    Code = mapping.Code,
                                    IsEquivalent = mapping.IsEquivalent,
                                    MapsToCode = mapping.MapsToCode,
                                    MapsToTitle = mapping.MapsToTitle,
                                    Notes = mapping.Notes,
                                    Title = mapping.Title
                                };

                                // De-duplicate within the same batch
                                pageByKey[record.MappingKey] = record;
                            }
                        }

                        var pageMappings = pageByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageMappings.Count} mappings) to Supabase...");
                        if (pageMappings.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageMappings.ToArray(), "mappings");
                        }
                        totalMappingsSaved += pageMappings.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total mappings saved: {totalMappingsSaved})");
                        Console.WriteLine();

                        allMappings.AddRange(pageMappings);
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
                Console.WriteLine($"✓ Saved {allMappings.Count} mappings to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allMappings;
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
                Console.WriteLine($"\nNote: {allMappings.Count} mappings were saved before the error occurred.\n");
                return allMappings.Count > 0 ? allMappings : null;
            }
        }

        private static string BuildMappingKey(string trainingComponentCode, Mapping mapping)
        {
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                mapping.Code ?? string.Empty,
                mapping.MapsToCode ?? string.Empty,
                mapping.MapsToTitle ?? string.Empty,
                mapping.IsEquivalent.ToString(),
                mapping.Title ?? string.Empty,
                mapping.Notes ?? string.Empty
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
