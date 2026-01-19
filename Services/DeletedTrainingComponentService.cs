using System;
using System.ServiceModel;
using System.Threading.Tasks;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching deleted training components from TGA (Training.gov.au) web services
    /// </summary>
    public class DeletedTrainingComponentService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of DeletedTrainingComponentService with default credentials
        /// </summary>
        public DeletedTrainingComponentService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = "WebService.Read";
            _trainingComponentClient.ClientCredentials.UserName.Password = "Asdf098";
        }

        /// <summary>
        /// Initializes a new instance of DeletedTrainingComponentService with custom credentials
        /// </summary>
        public DeletedTrainingComponentService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Searches deleted training components by deleted date and processes each page with a callback
        /// </summary>
        /// <param name="onPageFetched">Callback function called for each page of results (pageResults, pageNumber, totalSoFar)</param>
        /// <param name="startDate">Start date for search</param>
        /// <param name="endDate">End date for search</param>
        /// <param name="maxResults">Maximum number of results to return (0 = try to get all via pagination)</param>
        /// <param name="pageSize">Number of results per page</param>
        /// <returns>Total number of deleted components processed</returns>
        public async Task<int> SearchDeletedByDeletedDateWithCallback(
            Func<DeletedTrainingComponent[], int, int, Task> onPageFetched,
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

            Console.WriteLine($"  Searching deleted training components between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}");

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
                    var request = new DeletedSearchRequest
                    {
                        StartDate = startDateOffset,
                        EndDate = endDateOffset,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };

                    Console.WriteLine($"  Attempting search - Page {pageNumber}, PageSize {pageSize}...");

                    DeletedTrainingComponent[] results;
                    try
                    {
                        results = _trainingComponentClient.SearchDeletedByDeletedDate(request);
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

                            Console.WriteLine($"  ERROR: Message size quota exceeded even with page size {pageSize}. Cannot proceed.");
                            break;
                        }

                        Console.WriteLine($"  ERROR during search: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"    Inner Exception: {ex.InnerException.Message}");
                        }
                        break;
                    }

                    if (results == null || results.Length == 0)
                    {
                        Console.WriteLine($"  No more results (page {pageNumber})");
                        break;
                    }

                    Console.WriteLine($"  Page {pageNumber}: Found {results.Length} deleted training components");

                    try
                    {
                        await onPageFetched(results, pageNumber, totalProcessed);
                        totalProcessed += results.Length;
                    }
                    catch (Exception callbackEx)
                    {
                        Console.WriteLine($"  ✗ ERROR saving page {pageNumber}: {callbackEx.Message}");
                        throw;
                    }

                    if (maxResults > 0 && totalProcessed >= maxResults)
                    {
                        break;
                    }

                    if (results.Length < pageSize)
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
                Console.WriteLine($"  ERROR in SearchDeletedByDeletedDateWithCallback: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"    Inner Exception: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine($"  Total deleted training components processed: {totalProcessed}");
            return totalProcessed;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DeletedTrainingComponentService));
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
