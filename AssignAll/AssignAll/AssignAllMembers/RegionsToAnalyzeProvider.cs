using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace AssignAll.AssignAllMembers
{
    internal static class RegionsToAnalyzeProvider
    {
        private static readonly Regex AssignAllDisableRegex = new Regex(@"^// AssignAll disable$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AssignAllEnableRegex = new Regex(@"^// AssignAll enable$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static RegionsToAnalyze GetRegionsToAnalyze(SyntaxNode rootNode)
        {
            IOrderedEnumerable<SyntaxTrivia> singleLineCommentsInEntireFile =
                rootNode
                    .DescendantTrivia()
                    .Where(x => x.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    .OrderBy(x => x.SpanStart);

            var enabledTextSpans = new List<TextSpan>();
            foreach (SyntaxTrivia comment in singleLineCommentsInEntireFile)
            {
                if (AssignAllEnableRegex.IsMatch(comment.ToString()))
                {
                    // Start of enable analyzer text span
                    enabledTextSpans.Add(new TextSpan(comment.SpanStart,
                        rootNode.FullSpan.End - comment.SpanStart + 1));
                }
                else if (AssignAllDisableRegex.IsMatch(comment.ToString()))
                {
                    // End of enable analyzer text span
                    TextSpan? currentEnabledTextSpan = enabledTextSpans.Cast<TextSpan?>().LastOrDefault();
                    if (currentEnabledTextSpan == null) continue;

                    var spanLength = comment.Span.Start - currentEnabledTextSpan.Value.Start;

                    // Update TextSpan in list
                    enabledTextSpans.RemoveAt(enabledTextSpans.Count - 1);
                    enabledTextSpans.Add(new TextSpan(currentEnabledTextSpan.Value.Start, spanLength));
                }
            }

            return new RegionsToAnalyze(enabledTextSpans.ToImmutableArray());
        }
    }
}