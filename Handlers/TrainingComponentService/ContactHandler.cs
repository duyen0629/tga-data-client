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
    /// Handler for Contact operations - fetching and saving to database
    /// </summary>
    public static class ContactHandler
    {
        /// <summary>
        /// Fetches contacts via GetDetails for each training component code
        /// </summary>
        public static async Task<List<ContactRecord>> ProcessContacts(
            TrainingComponentSummaryService summaryService,
            ContactService contactService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Contacts ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allContacts = new List<ContactRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalContactsSaved = 0;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageContactsByKey = new Dictionary<string, ContactRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var contacts = contactService.GetContacts(summary.Code);
                            foreach (var contact in contacts)
                            {
                                var postal = contact.PostalAddress;
                                var record = new ContactRecord
                                {
                                    ContactKey = BuildContactKey(summary.Code, contact),
                                    TrainingComponentCode = summary.Code,
                                    RoleCode = contact.RoleCode,
                                    TypeCode = contact.TypeCode,
                                    FirstName = contact.FirstName,
                                    LastName = contact.LastName,
                                    OrganisationName = contact.OrganisationName,
                                    Email = contact.Email,
                                    Phone = contact.Phone,
                                    Mobile = contact.Mobile,
                                    Fax = contact.Fax,
                                    GroupName = contact.GroupName,
                                    JobTitle = contact.JobTitle,
                                    Title = contact.Title,
                                    PostalCountryCode = postal?.CountryCode,
                                    PostalLine1 = postal?.Line1,
                                    PostalLine2 = postal?.Line2,
                                    PostalSuburb = postal?.Suburb,
                                    PostalStateCode = postal?.StateCode,
                                    PostalStateOverseas = postal?.StateOverseas,
                                    PostalPostcode = postal?.Postcode,
                                    ActionOnEntity = contact.ActionOnEntity.ToString(),
                                    StartDate = contact.StartDate,
                                    EndDate = contact.EndDate
                                };

                                // De-duplicate within the same batch to avoid ON CONFLICT affecting a row twice
                                pageContactsByKey[record.ContactKey] = record;
                            }
                        }

                        var pageContacts = pageContactsByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageContacts.Count} contacts) to Supabase...");
                        if (pageContacts.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageContacts.ToArray(), "contacts");
                        }
                        totalContactsSaved += pageContacts.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total contacts saved: {totalContactsSaved})");
                        Console.WriteLine();

                        allContacts.AddRange(pageContacts);
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
                Console.WriteLine($"✓ Saved {allContacts.Count} contacts to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allContacts;
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
                Console.WriteLine($"\nNote: {allContacts.Count} contacts were saved before the error occurred.\n");
                return allContacts.Count > 0 ? allContacts : null;
            }
        }

        private static string BuildContactKey(string trainingComponentCode, Contact contact)
        {
            var postal = contact.PostalAddress;
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                contact.RoleCode ?? string.Empty,
                contact.TypeCode ?? string.Empty,
                contact.FirstName ?? string.Empty,
                contact.LastName ?? string.Empty,
                contact.OrganisationName ?? string.Empty,
                contact.Email ?? string.Empty,
                contact.Phone ?? string.Empty,
                contact.Mobile ?? string.Empty,
                contact.Fax ?? string.Empty,
                contact.GroupName ?? string.Empty,
                contact.JobTitle ?? string.Empty,
                contact.Title ?? string.Empty,
                contact.StartDate ?? string.Empty,
                contact.EndDate ?? string.Empty,
                postal?.CountryCode ?? string.Empty,
                postal?.Line1 ?? string.Empty,
                postal?.Line2 ?? string.Empty,
                postal?.Suburb ?? string.Empty,
                postal?.StateCode ?? string.Empty,
                postal?.StateOverseas ?? string.Empty,
                postal?.Postcode ?? string.Empty
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
