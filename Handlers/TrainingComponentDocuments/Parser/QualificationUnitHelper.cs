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
            var asterisk = 0;
            if (!string.IsNullOrEmpty(title))
            {
                var i = 0;
                while (i < title.Length && title[i] == '*')
                {
                    asterisk++;
                    i++;
                }
                if (asterisk > 0)
                {
                    title = title.Substring(i).TrimStart(' ');
                }
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
