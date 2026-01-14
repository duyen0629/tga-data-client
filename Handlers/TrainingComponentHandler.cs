using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers
{
    /// <summary>
    /// Handler for TrainingComponent operations - fetching and displaying details
    /// </summary>
    public static class TrainingComponentHandler
    {
        /// <summary>
        /// Fetches and displays TrainingComponent details for a given code
        /// </summary>
        /// <param name="tgaService">TGA data service instance</param>
        /// <param name="trainingComponentCode">The training component code to fetch</param>
        /// <param name="recognitionManagers">Optional array of RecognitionManagers for lookup (can be null)</param>
        /// <param name="showReleases">Whether to show release information</param>
        /// <param name="showRecognitionManagers">Whether to show recognition manager assignments</param>
        /// <param name="showContacts">Whether to show contact information</param>
        /// <param name="showClassifications">Whether to show classification information</param>
        /// <param name="showFullStructure">Whether to dump the full component structure (for debugging)</param>
        /// <returns>The TrainingComponent object (or null if not found)</returns>
        public static TrainingComponent ProcessTrainingComponentDetails(
            TgaDataService tgaService,
            string trainingComponentCode,
            RecognitionManager[] recognitionManagers = null,
            bool showReleases = true,
            bool showRecognitionManagers = true,
            bool showContacts = true,
            bool showClassifications = true,
            bool showFullStructure = true)
        {
            Console.WriteLine($"=== Training Component Details for Code: {trainingComponentCode} ===");

            var component = tgaService.GetTrainingComponentDetails(
                trainingComponentCode,
                showReleases: showReleases,
                showRecognitionManagers: showRecognitionManagers,
                showContacts: showContacts,
                showClassifications: showClassifications
            );

            if (component != null)
            {
                Console.WriteLine($"Code: {component.Code}");
                Console.WriteLine($"Title: {component.Title}");
                Console.WriteLine($"Component Type: {component.ComponentType}");
                Console.WriteLine();

                // Get Release Date(s)
                if (showReleases)
                {
                    Console.WriteLine("--- Releases (Release Dates) ---");
                    if (component.Releases != null && component.Releases.Length > 0)
                    {
                        foreach (var release in component.Releases)
                        {
                            Console.WriteLine($"Release Number: {release.ReleaseNumber}");
                            Console.WriteLine($"Release Date: {release.ReleaseDate}");
                            Console.WriteLine($"Currency: {release.Currency}");
                            if (!string.IsNullOrEmpty(release.IscApprovalDate))
                                Console.WriteLine($"ISC Approval Date: {release.IscApprovalDate}");
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine("No releases found.");
                        Console.WriteLine();
                    }
                }

                // Get Recognition Manager Assignments
                if (showRecognitionManagers)
                {
                    Console.WriteLine("--- Recognition Manager Assignments ---");
                    if (component.RecognitionManagers != null && component.RecognitionManagers.Length > 0)
                    {
                        foreach (var rmAssignment in component.RecognitionManagers)
                        {
                            Console.WriteLine($"Recognition Manager Code: {rmAssignment.Code}");
                            Console.WriteLine($"Start Date: {rmAssignment.StartDate}");
                            Console.WriteLine($"End Date: {rmAssignment.EndDate}");

                            // Look up the full details (Description) from the recognition managers list
                            if (recognitionManagers != null)
                            {
                                var rmDetails = recognitionManagers.FirstOrDefault(r => r.Code == rmAssignment.Code);
                                if (rmDetails != null)
                                {
                                    Console.WriteLine($"Recognition Manager Description: {rmDetails.Description}");
                                    Console.WriteLine($"Recognition Manager ShortName: {rmDetails.ShortName}");
                                }
                            }
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine("No recognition managers assigned to this component.");
                        Console.WriteLine();
                    }
                }

                // Show full component structure (optional - for debugging)
                if (showFullStructure)
                {
                    Console.WriteLine("--- Full Component Structure (for reference) ---");
                    Dump(component);
                }

                return component;
            }
            else
            {
                Console.WriteLine($"Training component with code '{trainingComponentCode}' not found.");
                return null;
            }
        }

        // Helper method to dump objects
        private static void Dump(object obj, int indent = 0, int depth = 0, int maxDepth = 4)
        {
            if (obj == null) { Indent(indent); Console.WriteLine("null"); return; }
            if (depth > maxDepth) { Indent(indent); Console.WriteLine("…"); return; }

            var t = obj.GetType();

            // Primitive-ish / strings
            if (t.IsPrimitive || obj is string || obj is DateTime || obj is decimal || obj is Guid || obj is TimeSpan)
            {
                Indent(indent); Console.WriteLine(obj);
                return;
            }

            // IEnumerable (arrays, lists)
            if (obj is IEnumerable en && !(obj is string))
            {
                int i = 0;
                foreach (var item in en)
                {
                    Indent(indent); Console.WriteLine($"[{i++}]");
                    Dump(item, indent + 2, depth + 1, maxDepth);
                }
                if (i == 0)
                {
                    Indent(indent); Console.WriteLine("(empty)");
                }
                return;
            }

            // Complex object: show public props
            Indent(indent); Console.WriteLine(t.FullName);
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.CanRead);
            foreach (var p in props)
            {
                object val;
                try { val = p.GetValue(obj, null); }
                catch { val = "(unreadable)"; }

                Indent(indent + 2); Console.WriteLine($"{p.Name}:");
                Dump(val, indent + 4, depth + 1, maxDepth);
            }
        }

        private static void Indent(int n) => Console.Write(new string(' ', n));
    }
}
