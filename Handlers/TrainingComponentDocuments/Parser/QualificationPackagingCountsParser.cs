using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    internal static class QualificationPackagingCountsParser
    {
        internal static void Parse(
            IEnumerable<XElement> paragraphs,
            XNamespace ns,
            Dictionary<string, object> packagingRules)
        {
            foreach (var p in paragraphs ?? new List<XElement>())
            {
                var text = CommonParser.ExtractInlineText(p) ?? string.Empty;

                // Format: "17 units of competency"
                var totalMatch = Regex.Match(text, @"(\d+)\s*units?\s*of\s*competency", RegexOptions.IgnoreCase);
                if (totalMatch.Success && int.TryParse(totalMatch.Groups[1].Value, out var totalUnits))
                {
                    packagingRules["total_units"] = totalUnits;
                }

                // Alternative format: "Total number of units = 36"
                var totalAltMatch = Regex.Match(text, @"Total\s+number\s+of\s+units\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                if (totalAltMatch.Success && int.TryParse(totalAltMatch.Groups[1].Value, out var totalUnitsAlt))
                {
                    packagingRules["total_units"] = totalUnitsAlt;
                }

                var coreMatch = Regex.Match(text, @"(\d+)\s*core\s*units?", RegexOptions.IgnoreCase);
                if (coreMatch.Success && int.TryParse(coreMatch.Groups[1].Value, out var coreRequired))
                {
                    packagingRules["core_units_required"] = coreRequired;
                }

                var electiveMatch = Regex.Match(text, @"(\d+)\s*elective\s*units?", RegexOptions.IgnoreCase);
                if (!electiveMatch.Success)
                {
                    electiveMatch = Regex.Match(text, @"(\d+)\s*electives?", RegexOptions.IgnoreCase);
                }
                if (electiveMatch.Success && int.TryParse(electiveMatch.Groups[1].Value, out var electiveRequired))
                {
                    packagingRules["elective_units_required"] = electiveRequired;
                }
            }
        }
    }
}
