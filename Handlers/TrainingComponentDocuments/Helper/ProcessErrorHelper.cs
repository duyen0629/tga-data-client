using System;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Helper
{
    internal static class ProcessErrorHelper
    {
        internal static bool IsStatementTimeout(Exception ex)
        {
            var message = ex?.Message ?? string.Empty;
            return message.IndexOf("57014", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("statement timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string BuildProcessError(Exception ex)
        {
            if (ex == null)
            {
                return "Unknown error.";
            }

            var innerMessage = ex.InnerException != null ? ex.InnerException.Message : null;
            var typeName = ex.GetType().Name;
            var summary = $"[{typeName}] {ex.Message}";

            if (!string.IsNullOrWhiteSpace(innerMessage))
            {
                summary += $" | Inner: {innerMessage}";
            }

            return summary;
        }
    }
}
