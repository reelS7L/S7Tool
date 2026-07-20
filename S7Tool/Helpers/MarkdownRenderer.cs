using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace S7Tool.Helpers
{
    public static class MarkdownRenderer
    {
        public static FlowDocument Render(string text)
        {
            var doc = new FlowDocument();

            if (string.IsNullOrWhiteSpace(text))
                return doc;

            text = text.Replace("\r", "");

            var lines = text.Split('\n');

            bool inCode = false;
            Paragraph? codeBlock = null;

            foreach (var lineRaw in lines)
            {
                var line = lineRaw ?? "";

                if (line.TrimStart().StartsWith("```"))
                {
                    inCode = !inCode;

                    if (inCode)
                    {
                        codeBlock = new Paragraph
                        {
                            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                            Background = (Brush)Application.Current.Resources["SurfaceBrush"],
                            Foreground = (Brush)Application.Current.Resources["AccentHoverBrush"],
                            Padding = new Thickness(10, 8, 10, 8),
                            Margin = new Thickness(0, 6, 0, 6)
                        };

                        doc.Blocks.Add(codeBlock);
                    }

                    continue;
                }

                if (inCode)
                {
                    codeBlock?.Inlines.Add(new Run(line + "\n"));
                    continue;
                }

                var p = new Paragraph();

                if (line.StartsWith("# "))
                {
                    p.Inlines.Add(new Run(line[2..])
                    {
                        FontSize = 22,
                        FontWeight = FontWeights.Bold
                    });
                }
                else if (line.StartsWith("## "))
                {
                    p.Inlines.Add(new Run(line[3..])
                    {
                        FontSize = 18,
                        FontWeight = FontWeights.Bold
                    });
                }
                else if (line.StartsWith("- "))
                {
                    p.Inlines.Add(new Run("• " + line[2..]));
                }
                else
                {
                    foreach (var inline in ParseInline(line))
                        p.Inlines.Add(inline);
                }

                doc.Blocks.Add(p);
            }

            return doc;
        }

        private static Inline[] ParseInline(string text)
        {
            var result = new List<Inline>();

            if (string.IsNullOrEmpty(text))
                return result.ToArray();

            var parts = Regex.Split(text, @"(\*\*.*?\*\*)");

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                if (part.StartsWith("**") && part.EndsWith("**") && part.Length > 4)
                {
                    result.Add(new Run(part.Substring(2, part.Length - 4))
                    {
                        FontWeight = FontWeights.Bold
                    });
                }
                else
                {
                    result.Add(new Run(part));
                }
            }

            return result.ToArray();
        }
    }
}