using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using training.gov.au.services;

namespace TgaGateway2
{
    internal class Program
    {
        static async Task Main(string[] args)
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

                    // 3. Save to Supabase
                    Console.WriteLine("=== Saving to Supabase ===");
                    try
                    {
                        await SaveRecognitionManagersToSupabase(recognitionManagers);
                        Console.WriteLine($"Successfully saved {recognitionManagers.Length} Recognition Managers to Supabase!\n");
                    }
                    catch (Exception supabaseEx)
                    {
                        Console.WriteLine($"ERROR: Failed to save to Supabase: {supabaseEx.Message}");
                        if (supabaseEx.InnerException != null)
                        {
                            Console.WriteLine($"Inner Exception: {supabaseEx.InnerException.Message}");
                        }
                        Console.WriteLine("Continuing with rest of the application...\n");
                    }
                }
                else
                {
                    Console.WriteLine("No recognition managers found.\n");
                }

                // 4. Get Training Component Details
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

        /// <summary>
        /// Escapes special JSON characters in a string
        /// </summary>
        static string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Converts PascalCase to snake_case (e.g., "ShortName" -> "short_name")
        /// </summary>
        static string ConvertToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var result = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]) && i > 0)
                {
                    result.Append('_');
                }
                result.Append(char.ToLowerInvariant(input[i]));
            }
            return result.ToString();
        }

        /// <summary>
        /// Saves RecognitionManagers data to Supabase using REST API
        /// </summary>
        static async Task SaveRecognitionManagersToSupabase(RecognitionManager[] recognitionManagers)
        {
            // Get Supabase credentials from config
            var supabaseUrl = ConfigurationManager.AppSettings["SupabaseUrl"];
            var supabaseKey = ConfigurationManager.AppSettings["SupabaseKey"];

            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
            {
                throw new Exception("Supabase URL and Key must be configured in App.config (appSettings section)");
            }

            // Remove trailing slash from URL if present
            if (supabaseUrl.EndsWith("/"))
            {
                supabaseUrl = supabaseUrl.TrimEnd('/');
            }

            // Build the REST API endpoint
            var endpointUrl = $"{supabaseUrl}/rest/v1/recognition_managers";

            // Build JSON array manually - using reflection to get ALL properties
            var jsonRecords = new List<string>();
            var utcNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var rmType = typeof(RecognitionManager);

            // Get all public properties with DataMemberAttribute (actual data fields)
            var dataProperties = rmType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttributes(typeof(System.Runtime.Serialization.DataMemberAttribute), false).Length > 0)
                .OrderBy(p => p.Name)
                .ToList();

            // Get ExtensionData property separately (it doesn't have DataMemberAttribute)
            var extensionDataProperty = rmType.GetProperty("ExtensionData", BindingFlags.Public | BindingFlags.Instance);

            foreach (var rm in recognitionManagers)
            {
                var jsonFields = new List<string>();

                // Process all data properties with DataMemberAttribute
                foreach (var prop in dataProperties)
                {
                    try
                    {
                        var value = prop.GetValue(rm);
                        var jsonValue = value?.ToString() ?? string.Empty;

                        // Convert property name from PascalCase to snake_case for database
                        var dbFieldName = ConvertToSnakeCase(prop.Name);

                        jsonFields.Add($"\"{dbFieldName}\":\"{EscapeJson(jsonValue)}\"");
                    }
                    catch (Exception ex)
                    {
                        // If property can't be read, skip it
                        Console.WriteLine($"Warning: Could not read property {prop.Name}: {ex.Message}");
                    }
                }

                // Process ExtensionData if present
                // ExtensionData is used by WCF for version tolerance - stores extra XML elements
                // from newer versions of the service contract
                if (extensionDataProperty != null)
                {
                    try
                    {
                        var extensionData = extensionDataProperty.GetValue(rm) as System.Runtime.Serialization.ExtensionDataObject;
                        bool hasData = false;
                        int elementCount = 0;
                        string extensionDataXml = null;

                        if (extensionData != null)
                        {
                            try
                            {
                                // ExtensionDataObject is used by WCF for version tolerance
                                // It stores extra XML elements from newer versions of the service contract
                                // Access its internal data using reflection

                                // Try to get the ExtensionElements property (internal collection)
                                var extensionElementsProp = extensionData.GetType().GetProperty("ExtensionElements",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                if (extensionElementsProp != null)
                                {
                                    var extensionElements = extensionElementsProp.GetValue(extensionData);

                                    if (extensionElements != null)
                                    {
                                        // Check if it's a collection with a Count property
                                        var countProp = extensionElements.GetType().GetProperty("Count",
                                            BindingFlags.Public | BindingFlags.Instance);

                                        if (countProp != null)
                                        {
                                            elementCount = (int)countProp.GetValue(extensionElements);
                                            hasData = elementCount > 0;

                                            // Try to enumerate if it implements IEnumerable
                                            if (extensionElements is IEnumerable enumerable && elementCount > 0)
                                            {
                                                var xmlStrings = new List<string>();
                                                foreach (var item in enumerable)
                                                {
                                                    if (item is XmlElement xmlElement)
                                                    {
                                                        try
                                                        {
                                                            var xmlString = xmlElement.OuterXml ?? xmlElement.InnerXml ?? string.Empty;
                                                            if (!string.IsNullOrWhiteSpace(xmlString))
                                                            {
                                                                xmlStrings.Add(xmlString);
                                                            }
                                                        }
                                                        catch
                                                        {
                                                            // Skip elements that can't be serialized
                                                        }
                                                    }
                                                }

                                                if (xmlStrings.Count > 0)
                                                {
                                                    extensionDataXml = string.Join(" || ", xmlStrings);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // ExtensionElements property not found - try alternative approach
                                    // Check if ExtensionData has a Count property directly
                                    var countProp = extensionData.GetType().GetProperty("Count",
                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                    if (countProp != null)
                                    {
                                        elementCount = (int)countProp.GetValue(extensionData);
                                        hasData = elementCount > 0;
                                    }
                                    else
                                    {
                                        // ExtensionDataObject is typically empty unless there's a version mismatch
                                        // If we can't access it, assume it's empty (which is normal)
                                        hasData = false;
                                        elementCount = 0;
                                    }
                                }
                            }
                            catch
                            {
                                // ExtensionDataObject access failed - this is normal, it's usually empty
                                // If we can't access it, assume it's empty
                                hasData = false;
                                elementCount = 0;
                            }
                        }

                        // Add ExtensionData fields to JSON
                        jsonFields.Add($"\"extension_data_present\":{(hasData ? "true" : "false")}");
                        jsonFields.Add($"\"extension_data_element_count\":{elementCount}");

                        if (hasData && !string.IsNullOrWhiteSpace(extensionDataXml))
                        {
                            jsonFields.Add($"\"extension_data\":\"{EscapeJson(extensionDataXml)}\"");
                        }
                        else
                        {
                            jsonFields.Add($"\"extension_data\":null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not read ExtensionData property: {ex.Message}");
                        jsonFields.Add($"\"extension_data_present\":false");
                        jsonFields.Add($"\"extension_data_element_count\":-1");
                        jsonFields.Add($"\"extension_data\":null");
                    }
                }
                else
                {
                    // ExtensionData property doesn't exist on this type
                    jsonFields.Add($"\"extension_data_present\":false");
                    jsonFields.Add($"\"extension_data_element_count\":0");
                    jsonFields.Add($"\"extension_data\":null");
                }

                // Add updated_at timestamp
                jsonFields.Add($"\"updated_at\":\"{utcNow}\"");

                var recordJson = "{" + string.Join(",", jsonFields) + "}";
                jsonRecords.Add(recordJson);
            }

            var json = "[" + string.Join(",", jsonRecords) + "]";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Create HTTP client and set headers
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("apikey", supabaseKey);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");
                httpClient.DefaultRequestHeaders.Add("Prefer", "resolution=merge-duplicates");
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Send POST request (upsert - merge on conflict)
                var response = await httpClient.PostAsync(endpointUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Supabase API error ({response.StatusCode}): {errorContent}");
                }
            }
        }
    }
}
