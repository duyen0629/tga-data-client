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
    /// Handler for Unit Grid operations - fetching and saving to database
    /// </summary>
    public static class UnitGridEntryHandler
    {
        /// <summary>
        /// Fetches unit grid entries via GetDetails for each training component code
        /// </summary>
        public static async Task<List<UnitGridEntryRecord>> ProcessUnitGridEntries(
            TrainingComponentSummaryService summaryService,
            ReleaseService releaseService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Unit Grid Entries ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allUnitGridEntries = new List<UnitGridEntryRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalUnitGridEntriesSaved = 0;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageByKey = new Dictionary<string, UnitGridEntryRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var releases = releaseService.GetReleases(summary.Code);
                            foreach (var release in releases)
                            {
                                var unitGrid = release.UnitGrid ?? Array.Empty<UnitGridEntry>();
                                foreach (var unit in unitGrid)
                                {
                                    var record = new UnitGridEntryRecord
                                    {
                                        UnitGridEntryKey = BuildUnitGridEntryKey(summary.Code, release, unit),
                                        TrainingComponentCode = summary.Code,
                                        ReleaseNumber = release.ReleaseNumber,
                                        ReleaseDate = release.ReleaseDate,
                                        ReleaseCurrency = release.Currency,
                                        UnitCode = unit.Code,
                                        UnitTitle = unit.Title
                                    };

                                    // De-duplicate within the same batch
                                    pageByKey[record.UnitGridEntryKey] = record;
                                }
                            }
                        }

                        var pageUnitGridEntries = pageByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageUnitGridEntries.Count} unit grid entries) to Supabase...");
                        if (pageUnitGridEntries.Count > 0)
                        {
                            const int batchSize = 200;
                            for (int i = 0; i < pageUnitGridEntries.Count; i += batchSize)
                            {
                                var batch = pageUnitGridEntries.Skip(i).Take(batchSize).ToArray();
                                await supabaseService.SaveToSupabase(batch, "unit_grid_entries");
                            }
                        }
                        totalUnitGridEntriesSaved += pageUnitGridEntries.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total unit grid entries saved: {totalUnitGridEntriesSaved})");
                        Console.WriteLine();

                        allUnitGridEntries.AddRange(pageUnitGridEntries);
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
                Console.WriteLine($"✓ Saved {allUnitGridEntries.Count} unit grid entries to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allUnitGridEntries;
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
                Console.WriteLine($"\nNote: {allUnitGridEntries.Count} unit grid entries were saved before the error occurred.\n");
                return allUnitGridEntries.Count > 0 ? allUnitGridEntries : null;
            }
        }

        private static string BuildUnitGridEntryKey(string trainingComponentCode, Release release, UnitGridEntry unit)
        {
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                release.ReleaseNumber ?? string.Empty,
                release.ReleaseDate ?? string.Empty,
                release.Currency ?? string.Empty,
                unit.Code ?? string.Empty,
                unit.Title ?? string.Empty
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
