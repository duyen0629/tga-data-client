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

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseKey}");

                var response = await httpClient.GetAsync(endpointUrl);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Supabase API error ({response.StatusCode}): {errorContent}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<List<ReleaseFileRow>>(json) ?? new List<ReleaseFileRow>();
            }
        }

        public async Task<List<ReleaseFileRow>> GetReleaseFilesPage(int limit, int offset)
        {
            var endpointUrl = $"{_supabaseUrl}/rest/v1/release_files" +
                              $"?select=training_component_code,release_number,relative_path" +
                              $"&limit={limit}&offset={offset}";

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseKey}");

                var response = await httpClient.GetAsync(endpointUrl);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Supabase API error ({response.StatusCode}): {errorContent}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<List<ReleaseFileRow>>(json) ?? new List<ReleaseFileRow>();
            }
        }
    }

    public class ReleaseFileRow
    {
        public string training_component_code { get; set; }
        public string release_number { get; set; }
        public string relative_path { get; set; }
    }
}
