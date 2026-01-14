using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching TrainingComponentSummary data from TGA (Training.gov.au) web services
    /// </summary>
    public class TrainingComponentSummaryService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of TrainingComponentSummaryService with default credentials
        /// </summary>
        public TrainingComponentSummaryService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = "WebService.Read";
            _trainingComponentClient.ClientCredentials.UserName.Password = "Asdf098";
        }

        /// <summary>
        /// Initializes a new instance of TrainingComponentSummaryService with custom credentials
        /// </summary>
        public TrainingComponentSummaryService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Searches for training components by modified date and returns the summaries
        /// </summary>
        /// <param name="startDate">Start date for search (defaults to 10 years ago)</param>
        /// <param name="endDate">End date for search (defaults to now)</param>
        /// <param name="maxResults">Maximum number of results to return (0 = try to get all via pagination)</param>
        /// <returns>List of TrainingComponentSummary objects</returns>
        public List<TrainingComponentSummary> SearchByModifiedDate(DateTime? startDate = null, DateTime? endDate = null, int maxResults = 0)
        {
            EnsureNotDisposed();

            if (!startDate.HasValue)
                startDate = DateTime.Now.AddYears(-10); // Default to 10 years ago
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            // Convert to WCF DateTimeOffset struct (required by the API)
            var startDateUtc = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
            var endDateUtc = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);

            var startDateOffset = new System.DateTimeOffset
            {
                DateTime = startDateUtc,
                OffsetMinutes = 0
            };

            var endDateOffset = new System.DateTimeOffset
            {
                DateTime = endDateUtc,
                OffsetMinutes = 0
            };

            Console.WriteLine($"  Searching training components modified between {startDate.Value:yyyy-MM-dd} and {endDate.Value:yyyy-MM-dd}");

            var allSummaries = new List<TrainingComponentSummary>();
            int pageNumber = 1;
            int pageSize = 100; // Reduced page size to avoid message size quota errors

            try
            {
                while (true)
                {
                    var request = new TrainingComponentModifiedSearchRequest
                    {
                        StartDate = startDateOffset,
                        EndDate = endDateOffset,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };

                    Console.WriteLine($"  Attempting search - Page {pageNumber}, PageSize {pageSize}...");

                    TrainingComponentSearchResult searchResult;
                    try
                    {
                        searchResult = _trainingComponentClient.SearchByModifiedDate(request);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ERROR during search: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"    Inner Exception: {ex.InnerException.Message}");
                        }
                        break;
                    }

                    if (searchResult == null)
                    {
                        Console.WriteLine($"  Search returned null result");
                        break;
                    }

                    Console.WriteLine($"  Search result - Count: {searchResult.Count}, PageNumber: {searchResult.PageNumber}, PageSize: {searchResult.PageSize}");

                    if (searchResult.Results == null)
                    {
                        Console.WriteLine($"  Search result.Results is null");
                        break;
                    }

                    if (searchResult.Results.Length == 0)
                    {
                        Console.WriteLine($"  No results on page {pageNumber}");
                        break;
                    }

                    allSummaries.AddRange(searchResult.Results);

                    Console.WriteLine($"  Page {pageNumber}: Found {searchResult.Results.Length} training components (Total so far: {allSummaries.Count})");

                    // Check if we've reached the limit or end of results
                    if (maxResults > 0 && allSummaries.Count >= maxResults)
                    {
                        return allSummaries.Take(maxResults).ToList();
                    }

                    // If we got fewer results than page size, we've reached the end
                    if (searchResult.Results.Length < pageSize)
                    {
                        Console.WriteLine($"  Reached end of results (got {searchResult.Results.Length} < page size {pageSize})");
                        break;
                    }

                    pageNumber++;

                    // Safety limit to prevent infinite loops
                    if (pageNumber > 1000)
                    {
                        Console.WriteLine($"  Warning: Reached safety limit of 1000 pages. Stopping search.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR in SearchByModifiedDate: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"    Inner Exception: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine($"  Total training component summaries found: {allSummaries.Count}");
            return allSummaries;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TrainingComponentSummaryService));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _trainingComponentClient?.Close();
                _trainingComponentClient = null;
                _disposed = true;
            }
        }
    }
}
