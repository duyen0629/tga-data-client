using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    internal static class QualificationUnitHelper
    {
        internal static Dictionary<string, object> ParseCodeAndTitleFromCell(string cellText, string unitCodePattern)
        {
            if (string.IsNullOrWhiteSpace(cellText))
            {
                return null;
            }

            var match = Regex.Match(cellText, unitCodePattern);
            if (!match.Success)
            {
                return null;
            }

            var code = match.Groups[1].Value;
            var title = cellText.Substring(match.Index + match.Length).Trim().TrimStart('-', ':', ' ');
            var asterisk = false;
            if (!string.IsNullOrEmpty(title) && (title.StartsWith("*", StringComparison.Ordinal) || title.StartsWith(" *", StringComparison.Ordinal)))
            {
                asterisk = true;
                title = title.TrimStart(' ', '*').Trim();
            }
            return new Dictionary<string, object>
            {
                { "code", code },
                { "title", title ?? string.Empty },
                { "asterisk", asterisk }
            };
        }
    }
}
