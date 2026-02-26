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

                // "7 core and 3 elective" or "7 core and 3 elective units" - combined format
                var coreAndElectiveMatch = Regex.Match(text, @"(\d+)\s*core\s+and\s+(\d+)\s*elective(?:\s+units?)?", RegexOptions.IgnoreCase);
                if (coreAndElectiveMatch.Success && int.TryParse(coreAndElectiveMatch.Groups[1].Value, out var coreFromCombined) && int.TryParse(coreAndElectiveMatch.Groups[2].Value, out var electiveFromCombined))
                {
                    packagingRules["core_units_required"] = coreFromCombined;
                    packagingRules["elective_units_required"] = electiveFromCombined;
                }
                else
                {
                    var coreMatch = Regex.Match(text, @"(\d+)\s*core(?:\s+units?)?", RegexOptions.IgnoreCase);
                    if (coreMatch.Success && int.TryParse(coreMatch.Groups[1].Value, out var coreRequired))
                    {
                        packagingRules["core_units_required"] = coreRequired;
                    }

                    // Prefer summary format ("5 elective units." or "5 elective units, of which:") over detail phrases ("2 elective units from Group A")
                    // Also match "3 elective units." when followed by more text (e.g. ". Electives are to be chosen")
                    var electiveMatch = Regex.Match(text, @"(\d+)\s*elective\s*units?(?:\.?\s*$|\.\s|,\s*of\s*which)", RegexOptions.IgnoreCase);
                    if (!electiveMatch.Success)
                    {
                        electiveMatch = Regex.Match(text, @"(\d+)\s*electives?(?:\.?\s*$|\.\s|,\s*of\s*which)", RegexOptions.IgnoreCase);
                    }
                    if (electiveMatch.Success && int.TryParse(electiveMatch.Groups[1].Value, out var electiveRequired))
                    {
                        packagingRules["elective_units_required"] = electiveRequired;
                    }
                }
            }
        }
    }
}
