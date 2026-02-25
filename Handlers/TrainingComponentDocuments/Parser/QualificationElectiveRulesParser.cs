using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    /// <summary>
    /// Extracts paragraph elements that form the elective rules (all content above "Core Units" heading).
    /// </summary>
    internal static class QualificationElectiveRulesParser
    {
        internal static List<XElement> CollectElectiveRulesParagraphs(XElement textNode, XNamespace ns)
        {
            var result = new List<XElement>();

            if (textNode == null)
            {
                return result;
            }

            foreach (var element in textNode.Elements())
            {
                if (element.Name != ns + "p")
                {
                    continue;
                }
                var text = (CommonParser.ExtractInlineText(element) ?? string.Empty).Trim();
                if (text.Equals("Core Units", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                result.Add(element);
            }

            return result;
        }
    }
}
