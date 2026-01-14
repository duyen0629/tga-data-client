using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Create TrainingComponentService client
                var client = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
                client.ClientCredentials.UserName.UserName = "WebService.Read";
                client.ClientCredentials.UserName.Password = "Asdf098";

                Console.WriteLine("=== Training Component Service Demo ===\n");

                // 1. Get Server Time
                var serverTime = client.GetServerTime();
                Console.WriteLine($"Server time: {serverTime}\n");

                // 2. Get All Recognition Managers (with Description)
                Console.WriteLine("=== Recognition Managers ===");
                var recognitionManagers = client.GetRecognitionManagers();
                if (recognitionManagers != null && recognitionManagers.Length > 0)
                {
                    foreach (var rm in recognitionManagers)
                    {
                        Console.WriteLine($"Code: {rm.Code}");
                        Console.WriteLine($"Description: {rm.Description}");
                        Console.WriteLine($"ShortName: {rm.ShortName}");
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine("No recognition managers found.\n");
                }

                // 3. Get Training Component Details
                // NOTE: Replace "BSB40520" with an actual training component code
                string trainingComponentCode = "BSB40520"; // Example code - change this!

                Console.WriteLine($"=== Training Component Details for Code: {trainingComponentCode} ===");

                var request = new TrainingComponentDetailsRequest
                {
                    Code = trainingComponentCode,
                    InformationRequest = new TrainingComponentInformationRequested
                    {
                        ShowReleases = true,              // To get ReleaseDate
                        ShowRecognitionManagers = true,    // To get RecognitionManager assignments
                        ShowContacts = true,
                        ShowClassifications = true
                    }
                };

                var component = client.GetDetails(request);

                if (component != null)
                {
                    Console.WriteLine($"Code: {component.Code}");
                    Console.WriteLine($"Title: {component.Title}");
                    Console.WriteLine($"Component Type: {component.ComponentType}");
                    Console.WriteLine();

                    // Get Release Date(s)
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

                    // Get Recognition Manager Assignments
                    Console.WriteLine("--- Recognition Manager Assignments ---");
                    if (component.RecognitionManagers != null && component.RecognitionManagers.Length > 0)
                    {
                        foreach (var rmAssignment in component.RecognitionManagers)
                        {
                            Console.WriteLine($"Recognition Manager Code: {rmAssignment.Code}");
                            Console.WriteLine($"Start Date: {rmAssignment.StartDate}");
                            Console.WriteLine($"End Date: {rmAssignment.EndDate}");

                            // Look up the full details (Description) from the recognition managers list
                            var rmDetails = recognitionManagers?.FirstOrDefault(r => r.Code == rmAssignment.Code);
                            if (rmDetails != null)
                            {
                                Console.WriteLine($"Recognition Manager Description: {rmDetails.Description}");
                                Console.WriteLine($"Recognition Manager ShortName: {rmDetails.ShortName}");
                            }
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine("No recognition managers assigned to this component.");
                        Console.WriteLine();
                    }

                    // Show full component structure (optional - for debugging)
                    Console.WriteLine("--- Full Component Structure (for reference) ---");
                    Dump(component);
                }
                else
                {
                    Console.WriteLine($"Training component with code '{trainingComponentCode}' not found.");
                }

                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:");
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    if (ex.InnerException.InnerException != null)
                    {
                        Console.WriteLine($"Inner Inner Exception: {ex.InnerException.InnerException.Message}");
                    }
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.Write("Press Enter to exit...");
            Console.ReadLine();
        }

        // Helper method to dump objects
        static void Dump(object obj, int indent = 0, int depth = 0, int maxDepth = 4)
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

        static void Indent(int n) => Console.Write(new string(' ', n));
    }
}
