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
                await OrganisationSummaryHandler.ProcessOrganisationSummaries(
                    summaryService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0,
                    pageSize: 500);
            }
        }
    }
}
