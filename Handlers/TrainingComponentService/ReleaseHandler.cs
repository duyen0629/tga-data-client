using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Models;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for Release operations - fetching and saving to database
    /// </summary>
    public static class ReleaseHandler
    {
        /// <summary>
        /// Fetches releases via GetDetails for each training component code
        /// </summary>
        public static async Task<List<ReleaseRecord>> ProcessReleases(
            TrainingComponentSummaryService summaryService,
            ReleaseService releaseService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Releases ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allReleases = new List<ReleaseRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var loggedDiagnostics = false;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageReleases = new List<ReleaseRecord>();
                        var componentsWithReleases = 0;
                        var sampleDetails = new List<string>();

                        foreach (var summary in pageResults)
                        {
                            var releases = releaseService.GetReleases(summary.Code);
                            if (releases.Length > 0)
                            {
                                componentsWithReleases++;
                            }
                            if (pageNumber == 1 && sampleDetails.Count < 5)
                            {
                                sampleDetails.Add($"{summary.Code} ({summary.ComponentType}) -> {releases.Length}");
                            }
                            foreach (var release in releases)
                            {
                                pageReleases.Add(new ReleaseRecord
                                {
                                    TrainingComponentCode = summary.Code,
                                    ReleaseNumber = release.ReleaseNumber,
                                    ReleaseDate = release.ReleaseDate,
                                    Currency = release.Currency,
                                    ApprovalProcess = release.ApprovalProcess,
                                    IscApprovalDate = release.IscApprovalDate,
                                    MinisterialAgreementDate = release.MinisterialAgreementDate,
                                    NqcEndorsementDate = release.NqcEndorsementDate,
                                    ComponentsCount = release.Components?.Length ?? 0,
                                    FilesCount = release.Files?.Length ?? 0,
                                    UnitGridCount = release.UnitGrid?.Length ?? 0
                                });
                            }
                        }

                        if (!loggedDiagnostics)
                        {
                            Console.WriteLine($"  Diagnostics: Page {pageNumber} has {componentsWithReleases} components with releases out of {pageResults.Length}.");
                            if (sampleDetails.Count > 0)
                            {
                                Console.WriteLine($"  Sample: {string.Join(", ", sampleDetails)}");
                            }
                            if (componentsWithReleases == 0)
                            {
                                Console.WriteLine("  Note: If this stays zero, releases may not be populated for these component types.");
                            }
                            Console.WriteLine();
                            loggedDiagnostics = true;
                        }

                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageReleases.Count} releases) to Supabase...");
                        if (pageReleases.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageReleases.ToArray(), "releases");
                        }
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total releases saved: {allReleases.Count + pageReleases.Count})");
                        Console.WriteLine();

                        allReleases.AddRange(pageReleases);
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
                Console.WriteLine($"✓ Saved {allReleases.Count} releases to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allReleases;
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
                Console.WriteLine($"\nNote: {allReleases.Count} releases were saved before the error occurred.\n");
                return allReleases.Count > 0 ? allReleases : null;
            }
        }
    }
}
