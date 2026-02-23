using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TgaGateway2.Handlers.TrainingComponentDocuments.Helper;
using TgaGateway2.Handlers.TrainingComponentDocuments.Type;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    // Shared parsing used by both Unit and Qualification: topic to section, inline text, tables, bullets.
    internal static class CommonParser
    {
        internal static string ExtractTitle(string code, List<string> lines)
        {
            if (lines == null) return null;
            foreach (var line in lines)
            {
                var trimmed = (line ?? string.Empty).Trim();
                if (trimmed.StartsWith(code + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring(code.Length).Trim();
                }
            }
            return null;
        }

        internal static string SanitizeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return json;
            return Regex.Replace(json, @"\bundefined\b", "null");
        }

        internal static string ExtractInlineText(XElement element)
        {
            if (element == null)
            {
                return string.Empty;
            }

            var text = string.Concat(element.DescendantNodes().OfType<XText>().Select(t => t.Value));
            return Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
        }

        internal static DocumentSection ParseTopicToSection(XElement topic, XNamespace ns, string sectionKey, string sectionTitle, int order)
        {
            var section = new DocumentSection
            {
                key = sectionKey,
                title = sectionTitle,
                order = order
            };

            if (ElementPerformanceCriteriaHelper.IsElementsPerformanceCriteriaSection(sectionKey))
            {
                section.table = ElementPerformanceCriteriaHelper.ParseElementsTableFromXml(topic, ns);
                return section;
            }

            var textNode = topic.Element(ns + "Text");
            if (textNode != null)
            {
                var items = new List<SectionItem>();
                SectionTable table = null;
                var itemOrder = 1;
                var bulletStack = new Stack<SectionItem>();
                string lastParagraphItemId = null;

                foreach (var node in textNode.Elements())
                {
                    if (node.Name == ns + "table")
                    {
                        if (TableHelper.GetTableMaxColumnsFromTable(node, ns) > 1 && TableHelper.GetTableRowCountFromTable(node, ns) > 1)
                        {
                            table = ParseGenericTableFromXmlTable(node, ns, sectionKey);
                            table.order = itemOrder++;
                        }
                        else
                        {
                            foreach (var p in node.Descendants(ns + "p"))
                            {
                                AddParagraphItem(items, p, sectionKey, ref itemOrder, bulletStack, ref lastParagraphItemId);
                            }
                        }
                        continue;
                    }

                    if (node.Name == ns + "p")
                    {
                        AddParagraphItem(items, node, sectionKey, ref itemOrder, bulletStack, ref lastParagraphItemId);
                    }
                }

                section.items = items;
                section.table = table;

                if (section.items.Count > 0 || section.table != null)
                {
                    return section;
                }
            }

            var fallbackTextLines = ExtractTextLinesFromTopic(topic, ns);
            section.items = ParseItems(fallbackTextLines, sectionKey);
            return section;
        }

        private static List<string> ExtractTextLinesFromTopic(XElement topic, XNamespace ns)
        {
            var textNode = topic.Element(ns + "Text");
            if (textNode == null)
            {
                return new List<string>();
            }

            var lines = new List<string>();
            foreach (var p in textNode.Descendants(ns + "p"))
            {
                var text = ExtractInlineText(p);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text.Trim());
                }
            }

            if (lines.Count > 0)
            {
                return lines;
            }

            var fallback = ExtractInlineText(textNode);
            return string.IsNullOrWhiteSpace(fallback) ? new List<string>() : new List<string> { fallback.Trim() };
        }

        private static SectionTable ParseGenericTableFromXmlTable(XElement table, XNamespace ns, string sectionKey)
        {
            var rows = new List<TableRowBase>();
            if (table == null)
            {
                return new SectionTable { columns = new List<string>(), rows = rows };
            }

            var tableRows = table.Elements(ns + "tr").ToList();
            if (tableRows.Count == 0)
            {
                return new SectionTable { columns = new List<string>(), rows = rows };
            }

            var headerIndex = -1;
            for (var i = 0; i < tableRows.Count; i++)
            {
                var headerAttr = tableRows[i].Attribute("header")?.Value;
                if (string.Equals(headerAttr, "true", StringComparison.OrdinalIgnoreCase))
                {
                    headerIndex = i;
                    break;
                }
            }

            var maxColumns = 0;
            foreach (var tr in tableRows)
            {
                var tdCount = tr.Elements(ns + "td")
                    .Select(td => TableHelper.GetSpanValue(td, "colspan"))
                    .Sum();
                if (tdCount > maxColumns)
                {
                    maxColumns = tdCount;
                }
            }

            var columns = new List<string>();
            if (headerIndex >= 0)
            {
                var headerCells = tableRows[headerIndex]
                    .Elements(ns + "td")
                    .ToList();

                foreach (var td in headerCells)
                {
                    var text = ExtractInlineText(td).Trim();
                    var span = TableHelper.GetSpanValue(td, "colspan");
                    if (span < 1)
                    {
                        span = 1;
                    }

                    columns.Add(string.IsNullOrWhiteSpace(text) ? $"Column {columns.Count + 1}" : text);
                    for (var i = 1; i < span; i++)
                    {
                        columns.Add($"Column {columns.Count + 1}");
                    }
                }
            }

            if (columns.Count == 0)
            {
                for (var i = 1; i <= Math.Max(1, maxColumns); i++)
                {
                    columns.Add($"Column {i}");
                }
            }

            var activeRowSpans = new List<RowSpanCell>();
            for (var i = 0; i < columns.Count; i++)
            {
                activeRowSpans.Add(null);
            }

            for (var i = 0; i < tableRows.Count; i++)
            {
                if (i == headerIndex)
                {
                    continue;
                }

                var cells = tableRows[i]
                    .Elements(ns + "td")
                    .ToList();

                if (cells.All(td => string.IsNullOrWhiteSpace(ExtractInlineText(td))))
                {
                    continue;
                }

                var rowCells = new List<List<SectionItem>>();
                for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    rowCells.Add(null);
                }

                var nextCellIndex = 0;
                var rowIndex = rows.Count + 1;

                for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    if (activeRowSpans[colIndex] == null)
                    {
                        continue;
                    }

                    rowCells[colIndex] = activeRowSpans[colIndex].Items;
                    activeRowSpans[colIndex].RemainingRows--;
                    if (activeRowSpans[colIndex].RemainingRows <= 0)
                    {
                        activeRowSpans[colIndex] = null;
                    }
                }

                for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    while (nextCellIndex < columns.Count && rowCells[nextCellIndex] != null)
                    {
                        nextCellIndex++;
                    }

                    if (nextCellIndex >= columns.Count)
                    {
                        break;
                    }

                    var cell = cells[cellIndex];
                    var cellItems = ParseCellItemsFromTd(cell, ns, sectionKey, rowIndex, nextCellIndex + 1);
                    var colspan = TableHelper.GetSpanValue(cell, "colspan");
                    if (colspan < 1)
                    {
                        colspan = 1;
                    }

                    for (var spanOffset = 0; spanOffset < colspan && (nextCellIndex + spanOffset) < columns.Count; spanOffset++)
                    {
                        rowCells[nextCellIndex + spanOffset] = spanOffset == 0
                            ? cellItems
                            : new List<SectionItem>();
                    }

                    var rowspan = TableHelper.GetSpanValue(cell, "rowspan");
                    if (rowspan > 1)
                    {
                        for (var spanOffset = 0; spanOffset < colspan && (nextCellIndex + spanOffset) < columns.Count; spanOffset++)
                        {
                            activeRowSpans[nextCellIndex + spanOffset] = new RowSpanCell(
                                spanOffset == 0 ? cellItems : new List<SectionItem>(),
                                rowspan - 1);
                        }
                    }

                    nextCellIndex += colspan;
                }

                rows.Add(new GenericTableRow { cells = rowCells });
            }

            return new SectionTable
            {
                columns = columns,
                rows = rows
            };
        }

        private static void AddParagraphItem(
            List<SectionItem> items,
            XElement p,
            string sectionKey,
            ref int order,
            Stack<SectionItem> bulletStack,
            ref string lastParagraphItemId)
        {
            var text = ExtractInlineText(p);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var idAttr = p.Attribute("id")?.Value;
            var indent = 0;
            var isBullet = false;

            if (int.TryParse(idAttr, out var idValue))
            {
                if (idValue >= 31016)
                {
                    isBullet = true;
                    indent = 2;
                }
                else if (idValue >= 14)
                {
                    isBullet = true;
                    indent = 1;
                }
                else if (idValue >= 13)
                {
                    isBullet = true;
                    indent = 0;
                }
            }

            var item = new SectionItem
            {
                item_id = $"{sectionKey}-{order}",
                type = isBullet ? "bullet" : "paragraph",
                text = text.Trim(),
                order = order++,
                indent = isBullet ? (int?)indent : null
            };
            if (isBullet)
            {
                item.parent_bullet_item_id = GetParentBulletIdForXml(bulletStack, indent, lastParagraphItemId);
                UpdateBulletStack(bulletStack, item);
            }
            else
            {
                lastParagraphItemId = item.item_id;
            }

            items.Add(item);
        }

        private static List<SectionItem> ParseCellItemsFromTd(
            XElement td,
            XNamespace ns,
            string sectionKey,
            int rowIndex,
            int columnIndex)
        {
            var items = new List<SectionItem>();
            var bulletStack = new Stack<SectionItem>();
            string lastParagraphItemId = null;
            var order = 1;

            var paragraphs = td.Descendants(ns + "p").ToList();
            if (paragraphs.Count == 0)
            {
                var text = ExtractInlineText(td);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var paragraphItem = new SectionItem
                    {
                        item_id = $"{sectionKey}_cell-{rowIndex}_{columnIndex}_{order}",
                        type = "paragraph",
                        text = text.Trim(),
                        order = order++,
                        indent = null
                    };
                    items.Add(paragraphItem);
                    lastParagraphItemId = paragraphItem.item_id;
                }
                return items;
            }

            foreach (var p in paragraphs)
            {
                var text = ExtractInlineText(p);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var idAttr = p.Attribute("id")?.Value;
                var indent = 0;
                var isBullet = false;

                if (int.TryParse(idAttr, out var idValue))
                {
                    if (idValue >= 31016)
                    {
                        isBullet = true;
                        indent = 2;
                    }
                    else if (idValue >= 14)
                    {
                        isBullet = true;
                        indent = 1;
                    }
                    else if (idValue >= 13)
                    {
                        isBullet = true;
                        indent = 0;
                    }
                }

                var cellItem = new SectionItem
                {
                    item_id = $"{sectionKey}_cell-{rowIndex}_{columnIndex}_{order}",
                    type = isBullet ? "bullet" : "paragraph",
                    text = text.Trim(),
                    order = order++,
                    indent = isBullet ? (int?)indent : null
                };
                if (isBullet)
                {
                    cellItem.parent_bullet_item_id = GetParentBulletIdForXml(bulletStack, indent, lastParagraphItemId);
                    UpdateBulletStack(bulletStack, cellItem);
                }
                else
                {
                    lastParagraphItemId = cellItem.item_id;
                }

                items.Add(cellItem);
            }

            return items;
        }

        private static List<SectionItem> ParseItems(List<string> lines, string sectionKey)
        {
            var items = new List<SectionItem>();
            SectionItem lastItem = null;
            var bulletStack = new Stack<SectionItem>();
            var order = 1;

            foreach (var rawLine in lines)
            {
                var line = rawLine ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (IsBulletLine(line, out var bulletText, out var indent))
                {
                    if (lastItem != null && lastItem.type == "bullet" && lastItem.text.EndsWith(":"))
                    {
                        indent = Math.Max(indent, (lastItem.indent ?? 0) + 1);
                    }

                    var item = new SectionItem
                    {
                        item_id = $"{sectionKey}-{order}",
                        type = "bullet",
                        text = bulletText,
                        order = order++,
                        indent = indent
                    };
                    item.parent_bullet_item_id = GetParentBulletId(bulletStack, indent);
                    items.Add(item);
                    lastItem = item;
                    UpdateBulletStack(bulletStack, item);
                    continue;
                }

                if (lastItem != null && lastItem.type == "bullet")
                {
                    lastItem.text = $"{lastItem.text} {line.Trim()}";
                    continue;
                }

                var paragraph = new SectionItem
                {
                    item_id = $"{sectionKey}-{order}",
                    type = "paragraph",
                    text = line.Trim(),
                    order = order++
                };
                items.Add(paragraph);
                lastItem = paragraph;
            }

            return items;
        }

        private static bool IsBulletLine(string line, out string text, out int indent)
        {
            text = null;
            indent = 0;

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var bulletChars = new[] { '•', '·', '•', '' };
            var index = line.IndexOfAny(bulletChars);
            if (index < 0)
            {
                return false;
            }

            var leadingSpaces = line.Take(index).Count(char.IsWhiteSpace);
            indent = leadingSpaces >= 4 ? 2 : (leadingSpaces >= 2 ? 1 : 0);

            var extracted = line.Substring(index + 1).Trim();
            if (string.IsNullOrWhiteSpace(extracted))
            {
                return false;
            }

            text = extracted;
            return true;
        }

        private static string GetParentBulletId(Stack<SectionItem> bulletStack, int indent)
        {
            if (indent <= 0 || bulletStack.Count == 0)
            {
                return null;
            }

            var items = bulletStack.ToList();
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var candidate = items[i];
                var candidateIndent = candidate.indent ?? 0;
                if (candidateIndent < indent)
                {
                    return candidate.item_id;
                }
            }

            return null;
        }

        private static string GetParentBulletIdForXml(
            Stack<SectionItem> bulletStack,
            int indent,
            string lastParagraphItemId)
        {
            if (indent <= 0)
            {
                return lastParagraphItemId;
            }

            return FindNearestBulletAtIndent(bulletStack, indent - 1);
        }

        private static string FindNearestBulletAtIndent(Stack<SectionItem> bulletStack, int targetIndent)
        {
            if (bulletStack == null || bulletStack.Count == 0)
            {
                return null;
            }

            var items = bulletStack.ToList();
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var candidate = items[i];
                var candidateIndent = candidate.indent ?? 0;
                if (candidateIndent == targetIndent)
                {
                    return candidate.item_id;
                }
            }

            return null;
        }

        private static void UpdateBulletStack(Stack<SectionItem> bulletStack, SectionItem item)
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
