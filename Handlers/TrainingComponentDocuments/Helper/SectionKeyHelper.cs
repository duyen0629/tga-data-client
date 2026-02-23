using System;
using System.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Helper
{
    /// <summary>
    /// Helpers for normalizing and comparing section keys (e.g. from titles).
    /// </summary>
    internal static class SectionKeyHelper
    {
        internal static string NormalizeKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var lowered = input.Trim().ToLowerInvariant();
            var normalized = lowered
                .Replace('-', '_')
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(' ', '_');

            var cleaned = new string(normalized.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            while (cleaned.Contains("__"))
            {
                cleaned = cleaned.Replace("__", "_");
            }

            return cleaned.Trim('_');
        }

        internal static bool SectionKeyEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
