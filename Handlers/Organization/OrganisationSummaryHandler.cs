using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TgaGateway2.Models;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.Organization
{
    /// <summary>
    /// Handler for Organisation summary operations - fetching and saving to database
    /// </summary>
    public static class OrganisationSummaryHandler
    {
        public static async Task<List<OrganisationSummaryRecord>> ProcessOrganisationSummaries(
            OrganisationSummaryService summaryService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0,
            int pageSize = 500)
        {
            Console.WriteLine("=== Getting and Saving Organisation Summaries ===");

            var allSummaries = new List<OrganisationSummaryRecord>();
            var saveStopwatch = Stopwatch.StartNew();

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageRecords = new List<OrganisationSummaryRecord>(pageResults.Length);
                        foreach (var item in pageResults)
                        {
                            var legacyItem = item as OrganisationSearchResultItem2;
                            var statusItem = item as OrganisationSearchResultItem3;

                            pageRecords.Add(new OrganisationSummaryRecord
                            {
                                Code = item.Code,
                                DataManagerCode = item.DataManagerCode,
                                HasActiveRegistration = item.HasActiveRegistration,
                                LegalPersonName = item.LegalPersonName,
                                TradingName = item.TradingName,
                                UpdatedDate = item.UpdatedDate,
                                IsLegacyData = legacyItem?.IsLegacyData ?? false,
                                RegistrationStatus = statusItem?.RegistrationStatus,
                                ExtensionData = item.ExtensionData
                            });
                        }

                        Console.WriteLine($"  Saving Page {pageNumber} ({pageRecords.Count} records) to Supabase...");
                        if (pageRecords.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageRecords.ToArray(), "organisation_summaries");
                        }
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total saved so far: {totalSoFar + pageRecords.Count})");
                        Console.WriteLine();

                        allSummaries.AddRange(pageRecords);
                    },
                    startDate,
                    endDate,
                    maxResults,
                    pageSize);

                if (totalProcessed == 0)
                {
                    Console.WriteLine("No organisation summaries found.\n");
                    return null;
                }

                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully processed and saved {totalProcessed} organisation summaries to Supabase!");
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
