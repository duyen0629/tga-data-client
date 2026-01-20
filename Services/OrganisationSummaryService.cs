using System;
using System.Threading.Tasks;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching Organisation summaries from TGA OrganisationService
    /// </summary>
    public class OrganisationSummaryService : IDisposable
    {
        private readonly TgaOrganisationService _organisationService;
        private bool _disposed = false;

        public OrganisationSummaryService()
        {
            _organisationService = new TgaOrganisationService();
        }

        public OrganisationSummaryService(string username, string password)
        {
            _organisationService = new TgaOrganisationService(username, password);
        }

        public async Task<int> SearchByModifiedDateWithCallback(
            Func<OrganisationSearchResultItem[], int, int, Task> onPageFetched,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0,
            int pageSize = 500)
        {
            EnsureNotDisposed();

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

            Console.WriteLine($"  Searching organisations modified between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}");

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
                    var request = new OrganisationModifiedSearchRequest
                    {
                        StartDate = startDateOffset,
                        EndDate = endDateOffset,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };

                    Console.WriteLine($"  Attempting search - Page {pageNumber}, PageSize {pageSize}...");

                    OrganisationSearchResult searchResult;
                    try
                    {
                        searchResult = _organisationService.SearchByModifiedDate(request);
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

                    if (searchResult == null || searchResult.Results == null || searchResult.Results.Length == 0)
                    {
                        Console.WriteLine($"  No more results (page {pageNumber})");
                        break;
                    }

                    Console.WriteLine($"  Page {pageNumber}: Found {searchResult.Results.Length} organisations");

                    try
                    {
                        await onPageFetched(searchResult.Results, pageNumber, totalProcessed);
                        totalProcessed += searchResult.Results.Length;
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

            Console.WriteLine($"  Total organisation summaries processed: {totalProcessed}");
            return totalProcessed;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OrganisationSummaryService));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _organisationService?.Dispose();
                _disposed = true;
            }
        }
    }
}
