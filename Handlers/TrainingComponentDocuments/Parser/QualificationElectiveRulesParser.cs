using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TgaGateway2.Handlers.TrainingComponentDocuments.Type;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    /// <summary>
    /// Extracts and parses paragraph elements that form the elective rules (all content above "Core Units" heading).
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

        /// <summary>
        /// Returns (type, content) for elective rules. When content has mixed bold (e.g. "27 elective units" bold, ", of which:" not),
        /// returns ("paragraph", content) so both stay on one row with inline formatting.
        /// </summary>
        internal static (string type, List<TextPart> content) GetElectiveRuleItemTypeAndContent(XElement p, XNamespace ns)
        {
            var idAttr = p.Attribute("id")?.Value;
            if (idAttr == "1021843")
            {
                return ("paragraph-bold", null);
            }
            if (idAttr == "10")
            {
                return ("header", null);
            }
            var contentParts = ExtractInlineContentWithBoldParts(p, ns);
            if (contentParts != null)
            {
                var hasBold = contentParts.Any(part => part.bold);
                var hasNonBold = contentParts.Any(part => !part.bold);
                if (hasBold && hasNonBold)
                {
                    if (int.TryParse(idAttr, out var idValue) && idValue >= 13)
                    {
                        return ("bullet", contentParts);
                    }
                    return ("paragraph", contentParts);
                }
                if (hasBold && contentParts.Count == 1)
                {
                    return ("paragraph-bold", null);
                }
                if (hasBold)
                {
                    return ("paragraph-bold", null);
                }
            }
            return (null, null);
        }

        /// <summary>
        /// Extracts inline content as parts; cs id="24" yields bold parts. Returns null if no cs id="24" present.
        /// </summary>
        private static List<TextPart> ExtractInlineContentWithBoldParts(XElement element, XNamespace ns)
        {
            if (!element.Descendants(ns + "cs").Any(cs => cs.Attribute("id")?.Value == "24"))
            {
                return null;
            }
            var parts = new List<TextPart>();
            foreach (var node in element.Nodes())
            {
                if (node is XText textNode)
                {
                    var t = (textNode.Value ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(t))
                    {
                        parts.Add(new TextPart { text = t, bold = false });
                    }
                }
                else if (node is XElement el)
                {
                    var elText = string.Concat(el.DescendantNodes().OfType<XText>().Select(t => t.Value)).Trim();
                    if (string.IsNullOrEmpty(elText))
                    {
                        continue;
                    }
                    var isBold = el.Name == ns + "cs" && el.Attribute("id")?.Value == "24";
                    parts.Add(new TextPart { text = elText, bold = isBold });
                }
            }
            return parts.Count > 0 ? parts : null;
        }

        internal static List<ElectiveRuleItem> ParseToItems(
            IEnumerable<XElement> paragraphElements,
            XNamespace ns,
            string sectionKey)
        {
            var items = new List<ElectiveRuleItem>();
            var bulletStack = new Stack<ElectiveRuleItem>();
            string lastParagraphItemId = null;
            var order = 1;

            foreach (var p in paragraphElements ?? Enumerable.Empty<XElement>())
            {
                var text = CommonParser.ExtractInlineText(p);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var idAttr = p.Attribute("id")?.Value;
                var indent = 0;
                var isBullet = false;

                (string itemType, List<TextPart> content) = GetElectiveRuleItemTypeAndContent(p, ns);

                if (string.IsNullOrEmpty(itemType))
                {
                    if (int.TryParse(idAttr, out var idValue))
                    {
                        if (idValue >= 31016)
                        {
                            isBullet = true;
                            indent = 2;
                            itemType = "bullet";
                        }
                        else if (idValue >= 14)
                        {
                            isBullet = true;
                            indent = 1;
                            itemType = "bullet";
                        }
                        else if (idValue >= 13)
                        {
                            isBullet = true;
                            indent = 0;
                            itemType = "bullet";
                        }
                        else
                        {
                            itemType = "paragraph";
                        }
                    }
                    else
                    {
                        itemType = "paragraph";
                    }
                }
                else if (itemType == "bullet")
                {
                    isBullet = true;
                    if (int.TryParse(idAttr, out var idValue))
                    {
                        indent = idValue >= 31016 ? 2 : (idValue >= 14 ? 1 : 0);
                    }
                }

                var item = new ElectiveRuleItem
                {
                    item_id = $"{sectionKey}-{order}",
                    type = itemType,
                    text = text.Trim(),
                    order = order++,
                    indent = isBullet ? (int?)indent : null,
                    content = content
                };
                if (isBullet)
                {
                    item.parent_bullet_item_id = GetParentBulletIdForElective(bulletStack, indent, lastParagraphItemId);
                    UpdateElectiveBulletStack(bulletStack, item);
                }
                else
                {
                    lastParagraphItemId = item.item_id;
                }

                items.Add(item);
            }

            return items;
        }

        private static string GetParentBulletIdForElective(Stack<ElectiveRuleItem> bulletStack, int indent, string lastParagraphItemId)
        {
            if (indent <= 0)
            {
                return lastParagraphItemId;
            }
            if (bulletStack == null || bulletStack.Count == 0)
            {
                return null;
            }
            var items = bulletStack.ToList();
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var candidate = items[i];
                var candidateIndent = candidate.indent ?? 0;
                if (candidateIndent == indent - 1)
                {
                    return candidate.item_id;
                }
            }
            return null;
        }

        private static void UpdateElectiveBulletStack(Stack<ElectiveRuleItem> bulletStack, ElectiveRuleItem item)
        {
            var indent = item.indent ?? 0;
            while (bulletStack.Count > 0)
            {
                var topIndent = bulletStack.Peek().indent ?? 0;
                if (topIndent < indent)
                {
                    break;
                }
                bulletStack.Pop();
            }
            bulletStack.Push(item);
        }
    }
}
