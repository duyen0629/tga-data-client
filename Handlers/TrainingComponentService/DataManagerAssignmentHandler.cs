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
    /// Handler for DataManagerAssignment operations - fetching and saving to database
    /// </summary>
    public static class DataManagerAssignmentHandler
    {
        /// <summary>
        /// Fetches data manager assignments via GetDetails for each training component code
        /// </summary>
        public static async Task<List<DataManagerAssignmentRecord>> ProcessDataManagerAssignments(
            TrainingComponentSummaryService summaryService,
            DataManagerAssignmentService assignmentService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Data Manager Assignments ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allAssignments = new List<DataManagerAssignmentRecord>();
            var saveStopwatch = Stopwatch.StartNew();

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageAssignments = new List<DataManagerAssignmentRecord>();

                        foreach (var summary in pageResults)
                        {
                            var assignments = assignmentService.GetDataManagerAssignments(summary.Code);
                            foreach (var assignment in assignments)
                            {
                                pageAssignments.Add(new DataManagerAssignmentRecord
                                {
                                    TrainingComponentCode = summary.Code,
                                    DataManagerCode = assignment.Code,
                                    ActionOnEntity = assignment.ActionOnEntity.ToString(),
                                    StartDate = assignment.StartDate,
                                    EndDate = assignment.EndDate
                                });
                            }
                        }

                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageAssignments.Count} assignments) to Supabase...");
                        if (pageAssignments.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageAssignments.ToArray(), "data_manager_assignments");
                        }
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total assignments saved: {allAssignments.Count + pageAssignments.Count})");
                        Console.WriteLine();

                        allAssignments.AddRange(pageAssignments);
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
                Console.WriteLine($"✓ Saved {allAssignments.Count} data manager assignments to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allAssignments;
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
                Console.WriteLine($"\nNote: {allAssignments.Count} assignments were saved before the error occurred.\n");
                return allAssignments.Count > 0 ? allAssignments : null;
            }
        }
    }
}
