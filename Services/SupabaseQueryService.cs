using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Lightweight Supabase query service for reading table rows.
    /// </summary>
    public class SupabaseQueryService
    {
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;

        public SupabaseQueryService()
        {
            _supabaseUrl = ConfigurationManager.AppSettings["SupabaseUrl"];
            _supabaseKey = ConfigurationManager.AppSettings["SupabaseKey"];

            if (string.IsNullOrEmpty(_supabaseUrl) || string.IsNullOrEmpty(_supabaseKey))
            {
                throw new Exception("Supabase URL and Key must be configured in App.config (appSettings section)");
            }

            if (_supabaseUrl.EndsWith("/"))
            {
                _supabaseUrl = _supabaseUrl.TrimEnd('/');
            }
        }

        public async Task<List<ReleaseFileRow>> GetReleaseFilesByCode(string trainingComponentCode)
        {
            if (string.IsNullOrWhiteSpace(trainingComponentCode))
            {
                throw new ArgumentException("Training component code is required.", nameof(trainingComponentCode));
            }

            var endpointUrl = $"{_supabaseUrl}/rest/v1/release_files" +
                              $"?select=training_component_code,release_number,relative_path" +
                              $"&training_component_code=eq.{Uri.EscapeDataString(trainingComponentCode)}";
            return await GetWithRetry(endpointUrl, "release_files by code");
        }

        public async Task<List<ReleaseFileRow>> GetReleaseFilesPage(int limit, int offset)
        {
            var endpointUrl = $"{_supabaseUrl}/rest/v1/release_files" +
                              $"?select=training_component_code,release_number,relative_path" +
                              $"&limit={limit}&offset={offset}";
            return await GetWithRetry(endpointUrl, $"release_files page (limit={limit}, offset={offset})");
        }

        private async Task<List<ReleaseFileRow>> GetWithRetry(string endpointUrl, string label)
        {
            var delaysMs = new[] { 1000, 2000, 4000 };
            Exception lastException = null;

            for (var attempt = 0; attempt <= delaysMs.Length; attempt++)
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
                        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseKey}");

                        var response = await httpClient.GetAsync(endpointUrl);
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            var exception = new Exception($"Supabase API error ({response.StatusCode}): {errorContent}");
                            if (!IsRetryableSupabaseError(exception))
                            {
                                throw exception;
                            }
                            throw exception;
                        }

                        var json = await response.Content.ReadAsStringAsync();
                        var serializer = new JavaScriptSerializer();
                        return serializer.Deserialize<List<ReleaseFileRow>>(json) ?? new List<ReleaseFileRow>();
                    }
                }
                catch (Exception ex) when (IsRetryableSupabaseError(ex) && attempt < delaysMs.Length)
                {
                    lastException = ex;
                    Console.WriteLine($"  ⚠ Supabase query failed for {label}. Retrying in {delaysMs[attempt]}ms...");
                    await Task.Delay(delaysMs[attempt]);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }
            }

            throw lastException ?? new Exception("Supabase query failed.");
        }

        private static bool IsRetryableSupabaseError(Exception ex)
        {
            var message = ex?.Message ?? string.Empty;
            return message.IndexOf("PGRST002", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("schema cache", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("ServiceUnavailable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("503", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public class ReleaseFileRow
    {
        public string training_component_code { get; set; }
        public string release_number { get; set; }
        public string relative_path { get; set; }
    }
}
