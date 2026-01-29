using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for saving data to Supabase database
    /// </summary>
    public class SupabaseService : IDisposable
    {
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of SupabaseService using configuration from App.config
        /// </summary>
        public SupabaseService()
        {
            _supabaseUrl = ConfigurationManager.AppSettings["SupabaseUrl"];
            _supabaseKey = ConfigurationManager.AppSettings["SupabaseKey"];

            if (string.IsNullOrEmpty(_supabaseUrl) || string.IsNullOrEmpty(_supabaseKey))
            {
                throw new Exception("Supabase URL and Key must be configured in App.config (appSettings section)");
            }

            // Remove trailing slash from URL if present
            if (_supabaseUrl.EndsWith("/"))
            {
                _supabaseUrl = _supabaseUrl.TrimEnd('/');
            }
        }

        /// <summary>
        /// Initializes a new instance of SupabaseService with explicit credentials
        /// </summary>
        public SupabaseService(string supabaseUrl, string supabaseKey)
        {
            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
            {
                throw new ArgumentException("Supabase URL and Key cannot be null or empty");
            }

            _supabaseUrl = supabaseUrl.TrimEnd('/');
            _supabaseKey = supabaseKey;
        }

        /// <summary>
        /// Saves an array of objects to Supabase table using reflection to extract properties
        /// </summary>
        /// <typeparam name="T">The type of objects to save</typeparam>
        /// <param name="items">Array of items to save</param>
        /// <param name="tableName">Name of the Supabase table (defaults to snake_case of type name)</param>
        public async Task SaveToSupabase<T>(T[] items, string tableName = null)
        {
            if (items == null || items.Length == 0)
            {
                throw new ArgumentException("Items array cannot be null or empty", nameof(items));
            }

            if (string.IsNullOrEmpty(tableName))
            {
                // Generate table name from type name (e.g., "RecognitionManager" -> "recognition_managers")
                tableName = ConvertToSnakeCase(Pluralize(typeof(T).Name));
            }

            var endpointUrl = $"{_supabaseUrl}/rest/v1/{tableName}";
            var json = BuildJsonFromObjects(items);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseKey}");
                httpClient.DefaultRequestHeaders.Add("Prefer", "resolution=merge-duplicates");
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var response = await httpClient.PostAsync(endpointUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Supabase API error ({response.StatusCode}): {errorContent}");
                }
            }
        }

        /// <summary>
        /// Builds JSON array from objects using reflection
        /// </summary>
        private string BuildJsonFromObjects<T>(T[] items)
        {
            var jsonRecords = new List<string>();
            var utcNow = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var itemType = typeof(T);

            // Get all public properties with DataMemberAttribute (actual data fields)
            var dataProperties = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttributes(typeof(DataMemberAttribute), false).Length > 0)
                .OrderBy(p => p.Name)
                .ToList();

            // Get ExtensionData property separately (it doesn't have DataMemberAttribute)
            var extensionDataProperty = itemType.GetProperty("ExtensionData", BindingFlags.Public | BindingFlags.Instance);

            foreach (var item in items)
            {
                var jsonFields = new List<string>();

                // Process all data properties with DataMemberAttribute
                foreach (var prop in dataProperties)
                {
                    try
                    {
                        var value = prop.GetValue(item);
                        var dbFieldName = MapFieldName(ConvertToSnakeCase(prop.Name));
                        string jsonValue;

                        // Handle WCF DateTimeOffset struct (has DateTime and OffsetMinutes properties)
                        if (value != null && prop.PropertyType.Name == "DateTimeOffset")
                        {
                            try
                            {
                                var dateTimeProp = prop.PropertyType.GetProperty("DateTime", BindingFlags.Public | BindingFlags.Instance);

                                if (dateTimeProp != null)
                                {
                                    var dateTimeValue = dateTimeProp.GetValue(value);
                                    if (dateTimeValue is DateTime dt)
                                    {
                                        // Format as ISO 8601 with UTC timezone
                                        jsonValue = dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                                    }
                                    else
                                    {
                                        jsonValue = string.Empty;
                                    }
                                }
                                else
                                {
                                    jsonValue = value?.ToString() ?? string.Empty;
                                }
                            }
                            catch
                            {
                                jsonValue = value?.ToString() ?? string.Empty;
                            }
                        }
                        // Handle nullable bool
                        else if (value != null && prop.PropertyType == typeof(bool?))
                        {
                            var boolValue = (bool?)value;
                            jsonValue = boolValue.HasValue ? boolValue.Value.ToString().ToLower() : "null";
                        }
                        // Handle enums
                        else if (value != null && prop.PropertyType.IsEnum)
                        {
                            jsonValue = value.ToString();
                        }
                        // Handle JsonRaw (write raw JSON without quotes)
                        if (value != null && prop.PropertyType == typeof(TgaGateway2.Models.JsonRaw))
                        {
                            var raw = ((TgaGateway2.Models.JsonRaw)value)?.Value;
                            raw = SanitizeRawJson(raw);
                            if (string.IsNullOrWhiteSpace(raw))
                            {
                                jsonFields.Add($"\"{dbFieldName}\":null");
                            }
                            else
                            {
                                jsonFields.Add($"\"{dbFieldName}\":{raw}");
                            }
                            continue;
                        }
                        // Handle null values
                        else if (value == null)
                        {
                            jsonValue = "null";
                        }
                        else
                        {
                            jsonValue = value.ToString() ?? string.Empty;
                        }

                        // Always add the field to keep object keys consistent across records
                        if (jsonValue == "null")
                        {
                            jsonFields.Add($"\"{dbFieldName}\":null");
                        }
                        else
                        {
                            jsonFields.Add($"\"{dbFieldName}\":\"{EscapeJson(jsonValue)}\"");
                        }
                    }
                    catch (Exception ex)
                    {
                        // If property can't be read, skip it
                        Console.WriteLine($"Warning: Could not read property {prop.Name}: {ex.Message}");
                    }
                }

                // Process ExtensionData if present
                ProcessExtensionData(extensionDataProperty, item, jsonFields);

                // Add fetched_updated_at timestamp
                jsonFields.Add($"\"fetched_updated_at\":\"{utcNow}\"");

                var recordJson = "{" + string.Join(",", jsonFields) + "}";
                jsonRecords.Add(recordJson);
            }

            return "[" + string.Join(",", jsonRecords) + "]";
        }

        /// <summary>
        /// Processes ExtensionData property for WCF version tolerance
        /// </summary>
        private void ProcessExtensionData<T>(PropertyInfo extensionDataProperty, T item, List<string> jsonFields)
        {
            if (extensionDataProperty == null)
            {
                return;
            }

            try
            {
                var extensionData = extensionDataProperty.GetValue(item) as ExtensionDataObject;
                bool hasData = false;
                int elementCount = 0;
                string extensionDataXml = null;

                if (extensionData != null)
                {
                    try
                    {
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
                                        foreach (var xmlItem in enumerable)
                                        {
                                            if (xmlItem is XmlElement xmlElement)
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
                            var countProp = extensionData.GetType().GetProperty("Count",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (countProp != null)
                            {
                                elementCount = (int)countProp.GetValue(extensionData);
                                hasData = elementCount > 0;
                            }
                            else
                            {
                                hasData = false;
                                elementCount = 0;
                            }
                        }
                    }
                    catch
                    {
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

        /// <summary>
        /// Escapes special JSON characters in a string
        /// </summary>
        private static string EscapeJson(string input)
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
        /// Ensures raw JSON values are valid (no 'undefined' tokens).
        /// </summary>
        private static string SanitizeRawJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            return Regex.Replace(input, @"\bundefined\b", "null");
        }

        /// <summary>
        /// Maps field names to correct database column names (handles typos in service contracts)
        /// </summary>
        private static string MapFieldName(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return fieldName;

            // Handle typo in TrainingComponentSummary.UsageReccomendation (one 'm')
            // Maps to correct spelling: usage_recommendation (two 'm's)
            if (string.Equals(fieldName, "usage_reccomendation", StringComparison.OrdinalIgnoreCase))
            {
                return "usage_recommendation";
            }

            return fieldName;
        }

        /// <summary>
        /// Converts PascalCase to snake_case (e.g., "ShortName" -> "short_name")
        /// </summary>
        private static string ConvertToSnakeCase(string input)
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
        /// Simple pluralization (adds 's' or 'es' - can be enhanced later)
        /// </summary>
        private static string Pluralize(string singular)
        {
            if (string.IsNullOrEmpty(singular))
                return singular;

            if (singular.EndsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                return singular.Substring(0, singular.Length - 1) + "ies";
            }
            else if (singular.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
                     singular.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
                     singular.EndsWith("z", StringComparison.OrdinalIgnoreCase) ||
                     singular.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
                     singular.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            {
                return singular + "es";
            }
            else
            {
                return singular + "s";
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // SupabaseService doesn't hold any unmanaged resources,
                // but we implement IDisposable for consistency with using statements
                _disposed = true;
            }
        }
    }
}
