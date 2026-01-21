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
    /// Handler for Release File operations - fetching and saving to database
    /// </summary>
    public static class ReleaseFileHandler
    {
        /// <summary>
        /// Fetches release files via GetDetails for each training component code
        /// </summary>
        public static async Task<List<ReleaseFileRecord>> ProcessReleaseFiles(
            TrainingComponentSummaryService summaryService,
            ReleaseService releaseService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Release Files ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allReleaseFiles = new List<ReleaseFileRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalReleaseFilesSaved = 0;
            const int supabaseBatchSize = 200;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageByKey = new Dictionary<string, ReleaseFileRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var releases = releaseService.GetReleases(summary.Code);
                            foreach (var release in releases)
                            {
                                var files = release.Files ?? Array.Empty<ReleaseFile>();
                                foreach (var file in files)
                                {
                                    var record = new ReleaseFileRecord
                                    {
                                        ReleaseFileKey = BuildReleaseFileKey(summary.Code, release, file),
                                        TrainingComponentCode = summary.Code,
                                        ReleaseNumber = release.ReleaseNumber,
                                        ReleaseDate = release.ReleaseDate,
                                        ReleaseCurrency = release.Currency,
                                        RelativePath = file.RelativePath,
                                        Size = file.Size
                                    };

                                    // De-duplicate within the same batch
                                    pageByKey[record.ReleaseFileKey] = record;
                                }
                            }
                        }

                        var pageReleaseFiles = pageByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageReleaseFiles.Count} release files) to Supabase...");
                        if (pageReleaseFiles.Count > 0)
                        {
                            for (int i = 0; i < pageReleaseFiles.Count; i += supabaseBatchSize)
                            {
                                var batch = pageReleaseFiles.Skip(i).Take(supabaseBatchSize).ToArray();
                                await supabaseService.SaveToSupabase(batch, "release_files");
                            }
                        }
                        totalReleaseFilesSaved += pageReleaseFiles.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total release files saved: {totalReleaseFilesSaved})");
                        Console.WriteLine();

                        allReleaseFiles.AddRange(pageReleaseFiles);
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
                Console.WriteLine($"✓ Saved {allReleaseFiles.Count} release files to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allReleaseFiles;
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
                Console.WriteLine($"\nNote: {allReleaseFiles.Count} release files were saved before the error occurred.\n");
                return allReleaseFiles.Count > 0 ? allReleaseFiles : null;
            }
        }

        private static string BuildReleaseFileKey(string trainingComponentCode, Release release, ReleaseFile file)
        {
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                release.ReleaseNumber ?? string.Empty,
                release.ReleaseDate ?? string.Empty,
                release.Currency ?? string.Empty,
                file.RelativePath ?? string.Empty,
                file.Size.ToString()
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
