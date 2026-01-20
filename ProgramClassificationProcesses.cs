using System.Threading.Tasks;
using TgaGateway2.Handlers.Classification;
using TgaGateway2.Services;

namespace TgaGateway2
{
    internal partial class Program
    {
        private static async Task ProcessClassificationSchemesFromClassificationService(SupabaseService supabaseService)
        {
            using (var schemeService = new ClassificationSchemeService())
            using (var classificationService = new TgaClassificationService())
            {
                await ClassificationSchemeSearchHandler.ProcessClassificationSchemes(
                    schemeService,
                    classificationService,
                    supabaseService);
            }
        }
    }
}
