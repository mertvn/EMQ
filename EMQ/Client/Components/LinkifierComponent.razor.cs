using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace EMQ.Client.Components
{
    public class LinkifierComponent : ComponentBase
    {
        [Parameter]
        public string Text { get; set; } = "";

        [Parameter]
        public string? CssClass { get; set; }

        [Parameter]
        public string Target { get; set; } = "_blank";

        private static readonly Regex s_tokenRegex = new(
            @"(https?:\/\/)([\w\-]+(\.[\w\-]+)+)([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?" +
            @"|~~(.+?)~~" +
            @"|\|\|(.+?)\|\|",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (string.IsNullOrEmpty(Text))
            {
                return;
            }

            int sequence = 0;
            int lastIndex = 0;

            foreach (Match match in s_tokenRegex.Matches(Text))
            {
                // Add text before the match
                if (match.Index > lastIndex)
                {
                    builder.AddMarkupContent(sequence++, EscapeHtml(Text[lastIndex..match.Index]));
                }

                if (match.Groups[5].Success)
                {
                    // Strikethrough: ~~text~~
                    builder.OpenElement(sequence++, "s");
                    builder.AddContent(sequence++, match.Groups[5].Value);
                    builder.CloseElement();
                }
                else if (match.Groups[6].Success)
                {
                    // Spoiler: ||text||
                    builder.OpenElement(sequence++, "span");
                    builder.AddAttribute(sequence++, "class", "spoiler");
                    builder.AddContent(sequence++, match.Groups[6].Value);
                    builder.CloseElement();
                }
                else
                {
                    // URL
                    builder.OpenElement(sequence++, "a");
                    builder.AddAttribute(sequence++, "href", match.Value);
                    builder.AddAttribute(sequence++, "target", Target);
                    builder.AddAttribute(sequence++, "rel", "noopener noreferrer");

                    if (!string.IsNullOrEmpty(CssClass))
                    {
                        builder.AddAttribute(sequence++, "class", CssClass);
                    }

                    builder.AddContent(sequence++, match.Value);
                    builder.CloseElement();
                }

                lastIndex = match.Index + match.Length;
            }

            // Add any remaining text after the last match
            if (lastIndex < Text.Length)
            {
                builder.AddMarkupContent(sequence, EscapeHtml(Text[lastIndex..]));
            }
        }

        private static string EscapeHtml(string text)
        {
            return text.Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
