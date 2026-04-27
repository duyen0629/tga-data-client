using System;
using System.Threading.Tasks;
using TgaGateway2.Handlers.Organization;
using TgaGateway2.Services;

namespace TgaGateway2
{
    internal partial class Program
    {
        private static async Task RunOrganisationServiceProcesses(SupabaseService supabaseService)
        {
            Console.WriteLine("=== Organisation Service Getting Data ===\n");

            using (var summaryService = new OrganisationSummaryService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                await OrganisationSummaryHandler.ProcessOrganisationSummaries(
                    summaryService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0,
                    pageSize: 500);
            }
        }
    }
}
