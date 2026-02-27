using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    /// <summary>
    /// Parses prerequisite requirements from qualification unit tables (Prerequisites column, inline prerequisite rows).
    /// </summary>
    internal static class QualificationUnitTablePrerequisites
    {
        private static readonly string[] PrereqRowPatterns = { "Prerequisite unit", "Prerequisiteunit", "Prerequisite_unit", "Prerequisite" };
        private const string PrereqLinePattern = @"^(\*+)\s*Prerequisite\s+unit\s+([A-Z]{2,10}\d{2,6}[A-Z]?)\s+(.+)$";

        internal static bool TryParseInlinePrerequisiteRow(
            List<XElement> tds,
            XNamespace ns,
            List<(string code, string title, int asteriskCount)> unitsWithAsteriskForPrereq,
            List<Dictionary<string, object>> inlinePrerequisites)
        {
            var rowText = string.Concat(tds.Select(td => CommonParser.ExtractInlineText(td))).Trim();
            if (!PrereqRowPatterns.Any(p => rowText.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0))
                return false;

            var unitsForMatching = new List<(string code, string title, int asteriskCount)>(unitsWithAsteriskForPrereq);
            foreach (var td in tds)
            {
                foreach (var p in td.Elements(ns + "p"))
                {
                    var lineText = (CommonParser.ExtractInlineText(p) ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(lineText)) continue;
                    var prereqMatch = Regex.Match(lineText, PrereqLinePattern, RegexOptions.IgnoreCase);
                    if (!prereqMatch.Success) continue;
                    var prereqAsteriskCount = prereqMatch.Groups[1].Value.Length;
                    var prereqCode = prereqMatch.Groups[2].Value;
                    var prereqTitle = prereqMatch.Groups[3].Value.Trim().TrimEnd('.');
                    var idx = unitsForMatching.FindIndex(u => u.asteriskCount == prereqAsteriskCount);
                    if (idx >= 0)
                    {
                        var unitMatch = unitsForMatching[idx];
                        unitsForMatching.RemoveAt(idx);
                        var prereqOrder = inlinePrerequisites.Count + 1;
                        inlinePrerequisites.Add(new Dictionary<string, object>
                        {
                            { "item_id", $"prerequisite_requirement-{prereqOrder}" },
                            { "unit_of_competency", new Dictionary<string, object> { { "code", unitMatch.code }, { "title", unitMatch.title }, { "asterisk", unitMatch.asteriskCount } } },
                            { "prerequisite_requirement", new List<Dictionary<string, object>> { new Dictionary<string, object> { { "code", prereqCode }, { "title", prereqTitle }, { "asterisk", 0 } } } }
                        });
                    }
                }
            }
            return true;
        }

        internal static List<Dictionary<string, object>> ParsePrerequisitesFromCell(XElement cell, XNamespace ns, string unitCodePattern)
        {
            var texts = new List<string>();
            foreach (var p in cell.Elements(ns + "p"))
            {
                var t = (CommonParser.ExtractInlineText(p) ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    texts.Add(t);
            }
            if (texts.Count == 0)
            {
                var fullText = (CommonParser.ExtractInlineText(cell) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(fullText))
                    return null;
                texts.AddRange(fullText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            var result = new List<Dictionary<string, object>>();
            for (var i = 0; i < texts.Count; i++)
            {
                var text = texts[i];
                var prereq = QualificationUnitHelper.ParseCodeAndTitleFromCell(text, unitCodePattern);
                if (prereq != null && !string.IsNullOrWhiteSpace(prereq["code"] as string))
                {
                    var title = (prereq["title"] as string) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(title) && i + 1 < texts.Count)
                    {
                        var nextText = texts[i + 1];
                        if (!Regex.IsMatch(nextText, unitCodePattern))
                        {
                            title = nextText.Trim();
                            i++;
                        }
                    }
                    result.Add(new Dictionary<string, object>
                    {
                        { "code", prereq["code"] },
                        { "title", title ?? string.Empty }
                    });
                }
            }
            return result.Count > 0 ? result : null;
        }
    }
}
