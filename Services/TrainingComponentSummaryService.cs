using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
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
            TgaSoapConfig.ApplyUserNameCredentials(_trainingComponentClient.ClientCredentials);
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
        /// Searches for training components by modified date and processes each page with a callback
        /// </summary>
        /// <param name="onPageFetched">Callback function called for each page of results (pageResults, pageNumber, totalSoFar)</param>
        /// <param name="startDate">Start date for search (defaults to 10 years ago)</param>
        /// <param name="endDate">End date for search (defaults to now)</param>
        /// <param name="maxResults">Maximum number of results to return (0 = try to get all via pagination)</param>
        /// <param name="pageSize">Number of results per page</param>
        /// <returns>Total number of summaries processed</returns>
        public async Task<int> SearchByModifiedDateWithCallback(
            Func<TrainingComponentSummary[], int, int, Task> onPageFetched,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0,
            int pageSize = 500)
        {
            EnsureNotDisposed();

            // Convert to WCF DateTimeOffset struct (required by the API)
            // Note: Using WCF DateTimeOffset from TgaTraining.cs (not System.DateTimeOffset)
            var startDateUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var endDateUtc = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

#pragma warning disable CS0436
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
#pragma warning restore CS0436

            Console.WriteLine($"  Searching training components modified between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}");

            int totalProcessed = 0;
            int pageNumber = 1;
            if (pageSize <= 0)
            {
                pageSize = 500;
            }

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
                        if (ex.Message.Contains("maximum message size quota") || ex.Message.Contains("MaxReceivedMessageSize"))
                        {
                            if (pageSize > 50)
                            {
                                pageSize = Math.Max(50, pageSize / 2);
                                Console.WriteLine($"  Message size quota exceeded. Reducing page size to {pageSize} and retrying page {pageNumber}...");
                                continue;
                            }
                            else
                            {
                                Console.WriteLine($"  ERROR: Message size quota exceeded even with page size {pageSize}. Cannot proceed.");
                                break;
                            }
                        }

                        Console.WriteLine($"  ERROR during search: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"    Inner Exception: {ex.InnerException.Message}");
                        }
                        break;
                    }

                    if (searchResult == null || searchResult.Results == null || searchResult.Results.Length == 0)
                    {
                        Console.WriteLine($"  No more results (page {pageNumber})");
                        break;
                    }

                    Console.WriteLine($"  Page {pageNumber}: Found {searchResult.Results.Length} training components");

                    // Process this page with the callback
                    try
                    {
                        await onPageFetched(searchResult.Results, pageNumber, totalProcessed);
                        totalProcessed += searchResult.Results.Length;
                    }
                    catch (Exception callbackEx)
                    {
                        Console.WriteLine($"  ✗ ERROR saving page {pageNumber}: {callbackEx.Message}");
                        throw; // Re-throw to stop processing
                    }

                    if (maxResults > 0 && totalProcessed >= maxResults)
                    {
                        break;
                    }

                    if (searchResult.Results.Length < pageSize)
                    {
                        Console.WriteLine($"  Reached end of results");
                        break;
                    }

                    pageNumber++;

                    if (pageNumber > 1000)
                    {
                        Console.WriteLine($"  Warning: Reached safety limit of 1000 pages.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR in SearchByModifiedDateWithCallback: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"    Inner Exception: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine($"  Total training component summaries processed: {totalProcessed}");
            return totalProcessed;
        }

        /// <summary>
        /// Fetches training component details by exact code via GetDetails (TGA API).
        /// Returns the full TrainingComponent, or null if not found.
        /// </summary>
        public TrainingComponent GetDetailsByCode(string code)
        {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var request = new TrainingComponentDetailsRequest
            {
                Code = code,
                InformationRequest = new TrainingComponentInformationRequested
                {
                    ShowUsageRecommendation = true
                }
            };

            try
            {
                return _trainingComponentClient.GetDetails(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR fetching details for {code}: {ex.Message}");
                return null;
            }
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
