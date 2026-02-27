using System;
using System.Text.RegularExpressions;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    /// <summary>
    /// Detects section headers and group headers in qualification unit tables (Core, Elective, Group A, etc.).
    /// </summary>
    internal static class QualificationUnitTableSectionMatcher
    {
        internal static readonly Regex GroupWithDelimiterRegex = new Regex(@"Group\s+([A-Za-z0-9]+)\s*[-:\u2013\u2014]\s*(.+)", RegexOptions.IgnoreCase);
        internal static readonly Regex GroupWithSpaceRegex = new Regex(@"Group\s+([A-Za-z0-9]+)\s+(.+)", RegexOptions.IgnoreCase);
        internal static readonly Regex GroupNoTitleRegex = new Regex(@"^Group\s+([A-Za-z0-9]+)\s*$", RegexOptions.IgnoreCase);
        internal static readonly Regex PrecedingGroupRegex = new Regex(@"^Group\s+([A-Za-z0-9]+)(?:\s*[-:\u2013\u2014]\s*(.+))?\s*$", RegexOptions.IgnoreCase);

        internal static bool IsCoreSectionHeader(string rowTrimmed)
        {
            if (string.Equals(rowTrimmed, "Core", StringComparison.OrdinalIgnoreCase))
                return true;
            if (rowTrimmed.StartsWith("Core units", StringComparison.OrdinalIgnoreCase))
                return true;
            // Handle split text e.g. "Core u nits" (XML has "Core </cs>u\n<cs>nits</cs>")
            var collapsed = Regex.Replace(rowTrimmed ?? string.Empty, @"\s+", "");
            return collapsed.StartsWith("Coreunits", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsElectiveSectionHeader(string rowTrimmed)
        {
            return rowTrimmed.StartsWith("Elective", StringComparison.OrdinalIgnoreCase)
                || (rowTrimmed.EndsWith("elective units", StringComparison.OrdinalIgnoreCase)
                    && !rowTrimmed.StartsWith("Core", StringComparison.OrdinalIgnoreCase)
                    && rowTrimmed.Length <= 60);
        }

        internal static bool IsCustomElectiveGroupHeader(string rowTrimmed)
        {
            if (GroupWithDelimiterRegex.Match(rowTrimmed).Success)
                return false;
            return rowTrimmed.EndsWith("elective units", StringComparison.OrdinalIgnoreCase)
                && !rowTrimmed.StartsWith("Elective", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetGroupKeyFromCustomElectiveHeader(string rowTrimmed)
        {
            var prefix = rowTrimmed.Substring(0, rowTrimmed.Length - " elective units".Length).Trim();
            return Regex.Replace(prefix, @"\s+", "") + "ElectiveUnits";
        }
    }
}
