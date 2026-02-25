using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    internal static class QualificationCoreUnitsParser
    {
        internal static List<Dictionary<string, object>> Parse(
            List<XElement> children,
            XNamespace ns,
            string unitCodePattern)
        {
            var result = new List<Dictionary<string, object>>();
            var coreUnitsStartIndex = -1;

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Name != ns + "p")
                {
                    continue;
                }
                var text = (CommonParser.ExtractInlineText(children[i]) ?? string.Empty).Trim();
                if (string.Equals(text, "Core Units", StringComparison.OrdinalIgnoreCase))
                {
                    coreUnitsStartIndex = i;
                    break;
                }
            }

            if (coreUnitsStartIndex < 0)
            {
                return result;
            }

            var order = 1;
            for (var i = coreUnitsStartIndex + 1; i < children.Count; i++)
            {
                var node = children[i];
                if (node.Name == ns + "table")
                {
                    break;
                }
                if (node.Name != ns + "p")
                {
                    continue;
                }

                var text = (CommonParser.ExtractInlineText(node) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    break;
                }
                if (text.IndexOf("Elective", StringComparison.OrdinalIgnoreCase) >= 0
                    || Regex.IsMatch(text, @"^Group\s+[A-Za-z0-9]+\s*$", RegexOptions.IgnoreCase))
                {
                    break;
                }

                var unitEntry = QualificationUnitHelper.ParseCodeAndTitleFromCell(text, unitCodePattern);
                if (unitEntry != null)
                {
                    unitEntry["item_id"] = $"core_unit-{order++}";
                    result.Add(unitEntry);
                }
            }

            return result;
        }
    }
}
