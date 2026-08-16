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

        private static readonly Regex s_nekobakoImageRegex = new(
            @"^https?://erogemusicquiz\.com/selfhoststorage/userup/nekobako/.+\.webp$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                else if (IsNekobakoImage(match.Value))
                {
                    // Image link
                    BuildImageTag(builder, ref sequence, match.Value);
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

        private void BuildImageTag(RenderTreeBuilder builder, ref int sequence, string imageUrl)
        {
            builder.OpenElement(sequence++, "a");
            builder.AddAttribute(sequence++, "href", imageUrl);
            builder.AddAttribute(sequence++, "target", Target);
            builder.AddAttribute(sequence++, "rel", "noopener noreferrer");

            if (!string.IsNullOrEmpty(CssClass))
            {
                builder.AddAttribute(sequence++, "class", CssClass);
            }

            builder.OpenElement(sequence++, "img");
            builder.AddAttribute(sequence++, "src", imageUrl);
            builder.AddAttribute(sequence++, "alt", "Nekobako image");
            builder.AddAttribute(sequence++, "loading", "lazy");
            builder.AddAttribute(sequence++, "style", "max-width: 200px; max-height: 200px; object-fit: contain;");
            builder.CloseElement();
            builder.CloseElement();
        }

        private static bool IsNekobakoImage(string url)
        {
            return s_nekobakoImageRegex.IsMatch(url);
        }

        private static string EscapeHtml(string text)
        {
            return text.Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
