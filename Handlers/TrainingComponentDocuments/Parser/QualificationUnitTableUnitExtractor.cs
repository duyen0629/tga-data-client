using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    /// <summary>
    /// Extracts unit entries from qualification unit table rows.
    /// </summary>
    internal static class QualificationUnitTableUnitExtractor
    {
        internal static (string title, int asterisk) NormalizeTitleAndAsterisk(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return (string.Empty, 0);
            var t = title.Trim().TrimStart('-', ':', ' ');
            var asterisk = 0;
            var i = 0;
            while (i < t.Length && t[i] == '*')
            {
                asterisk++;
                i++;
            }
            if (asterisk > 0)
                t = t.Substring(i).TrimStart(' ');
            return (t ?? string.Empty, asterisk);
        }

        internal static List<Dictionary<string, object>> ParseUnitRowsFromTable(
            XElement table,
            XNamespace ns,
            string unitCodePattern,
            string itemIdPrefix = null)
        {
            var result = new List<Dictionary<string, object>>();
            var rows = table.Elements(ns + "tr").ToList();
            var headerRowIndex = rows.FindIndex(r => string.Equals(r.Attribute("header")?.Value, "true", StringComparison.OrdinalIgnoreCase));
            var order = 1;

            for (var i = 0; i < rows.Count; i++)
            {
                if (i == headerRowIndex) continue;
                var tds = rows[i].Elements(ns + "td").ToList();
                var entry = ExtractUnitFromRow(tds, ns, unitCodePattern);
                if (entry == null) continue;

                if (!string.IsNullOrEmpty(itemIdPrefix))
                    entry["item_id"] = $"{itemIdPrefix}-{order++}";
                result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// Extracts cell text with newlines between paragraphs (preserves structure when cell has multiple &lt;p&gt; elements).
        /// </summary>
        private static string ExtractCellTextWithParagraphBreaks(XElement cell, XNamespace ns)
        {
            var paragraphs = cell.Elements(ns + "p")
                .Select(p => (CommonParser.ExtractInlineText(p) ?? string.Empty).Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
            if (paragraphs.Count > 1)
                return string.Join("\n", paragraphs);
            return paragraphs.Count > 0 ? paragraphs[0] : (CommonParser.ExtractInlineText(cell) ?? string.Empty).Trim();
        }

        /// <summary>
        /// Extracts a unit entry from table cells. Returns null if no unit code found.
        /// </summary>
        internal static Dictionary<string, object> ExtractUnitFromRow(
            List<XElement> tds,
            XNamespace ns,
            string unitCodePattern)
        {
            if (tds.Count < 2)
            {
                var singleText = tds.Count == 1 ? CommonParser.ExtractInlineText(tds[0]).Trim() : null;
                var codeMatch = singleText != null ? Regex.Match(singleText, unitCodePattern) : Match.Empty;
                if (!codeMatch.Success) return null;
                var code = codeMatch.Groups[1].Value;
                var rawTitle = singleText.Substring(codeMatch.Index + codeMatch.Length).Trim().TrimStart('-', ':', ' ');
                var (title, asterisk) = NormalizeTitleAndAsterisk(rawTitle ?? string.Empty);
                return new Dictionary<string, object> { { "code", code }, { "title", title }, { "asterisk", asterisk } };
            }

            var cellTexts = tds.Select(td => CommonParser.ExtractInlineText(td)).Select(t => (t ?? string.Empty).Trim()).ToList();
            for (var c = 0; c < cellTexts.Count; c++)
            {
                var cell = cellTexts[c];
                var match = Regex.Match(cell, unitCodePattern);
                if (!match.Success) continue;
                var unitCode = match.Groups[1].Value;
                string unitTitle;
                if (cell.Length > match.Index + match.Length)
                {
                    var sameCellTitle = cell.Substring(match.Index + match.Length).Trim().TrimStart('-', ':', ' ');
                    unitTitle = !string.IsNullOrWhiteSpace(sameCellTitle) ? sameCellTitle : null;
                }
                else
                {
                    unitTitle = null;
                }
                if (string.IsNullOrWhiteSpace(unitTitle) && c + 1 < tds.Count)
                    unitTitle = ExtractCellTextWithParagraphBreaks(tds[c + 1], ns);
                if (string.IsNullOrWhiteSpace(unitTitle) && cellTexts.Count > 1)
                    unitTitle = cellTexts[1];
                var (title, asterisk) = NormalizeTitleAndAsterisk(unitTitle ?? string.Empty);
                if (string.IsNullOrWhiteSpace(title) && cellTexts.Count > 1)
                    title = ExtractCellTextWithParagraphBreaks(tds[1], ns);
                return new Dictionary<string, object> { { "code", unitCode }, { "title", title }, { "asterisk", asterisk } };
            }
            return null;
        }
    }
}
