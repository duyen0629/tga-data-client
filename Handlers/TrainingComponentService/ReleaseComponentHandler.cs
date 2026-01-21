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
    /// Handler for Release Component operations - fetching and saving to database
    /// </summary>
    public static class ReleaseComponentHandler
    {
        /// <summary>
        /// Fetches release components via GetDetails for each training component code
        /// </summary>
        public static async Task<List<ReleaseComponentRecord>> ProcessReleaseComponents(
            TrainingComponentSummaryService summaryService,
            ReleaseService releaseService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Release Components ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allReleaseComponents = new List<ReleaseComponentRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalReleaseComponentsSaved = 0;
            const int supabaseBatchSize = 200;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageByKey = new Dictionary<string, ReleaseComponentRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var releases = releaseService.GetReleases(summary.Code);
                            foreach (var release in releases)
                            {
                                var components = release.Components ?? Array.Empty<ReleaseComponent>();
                                foreach (var component in components)
                                {
                                    var record = new ReleaseComponentRecord
                                    {
                                        ReleaseComponentKey = BuildReleaseComponentKey(summary.Code, release, component),
                                        TrainingComponentCode = summary.Code,
                                        ReleaseNumber = release.ReleaseNumber,
                                        ReleaseDate = release.ReleaseDate,
                                        ReleaseCurrency = release.Currency,
                                        ComponentCode = component.Code,
                                        ComponentTitle = component.Title,
                                        ComponentType = component.Type.ToString(),
                                        ComponentReleaseNumber = component.ReleaseNumber,
                                        ComponentReleaseDate = component.ReleaseDate,
                                        ComponentReleaseCurrency = component.ReleaseCurrency
                                    };

                                    // De-duplicate within the same batch
                                    pageByKey[record.ReleaseComponentKey] = record;
                                }
                            }
                        }

                        var pageReleaseComponents = pageByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageReleaseComponents.Count} release components) to Supabase...");
                        if (pageReleaseComponents.Count > 0)
                        {
                            for (int i = 0; i < pageReleaseComponents.Count; i += supabaseBatchSize)
                            {
                                var batch = pageReleaseComponents.Skip(i).Take(supabaseBatchSize).ToArray();
                                await supabaseService.SaveToSupabase(batch, "release_components");
                            }
                        }
                        totalReleaseComponentsSaved += pageReleaseComponents.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total release components saved: {totalReleaseComponentsSaved})");
                        Console.WriteLine();

                        allReleaseComponents.AddRange(pageReleaseComponents);
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
                Console.WriteLine($"✓ Saved {allReleaseComponents.Count} release components to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allReleaseComponents;
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
                Console.WriteLine($"\nNote: {allReleaseComponents.Count} release components were saved before the error occurred.\n");
                return allReleaseComponents.Count > 0 ? allReleaseComponents : null;
            }
        }

        private static string BuildReleaseComponentKey(string trainingComponentCode, Release release, ReleaseComponent component)
        {
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                release.ReleaseNumber ?? string.Empty,
                release.ReleaseDate ?? string.Empty,
                release.Currency ?? string.Empty,
                component.Code ?? string.Empty,
                component.ReleaseNumber ?? string.Empty,
                component.ReleaseDate ?? string.Empty,
                component.ReleaseCurrency ?? string.Empty,
                component.Title ?? string.Empty,
                component.Type.ToString()
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
