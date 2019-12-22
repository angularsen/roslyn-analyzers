using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AssignAll;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace AssignAll
{
    internal static class RegionsToAnalyzeProvider
    {
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
                string commentText = comment.ToString().Replace("//", "").Trim();
                if (commentText.Equals(AssignAllAnalyzer.CommentPattern_Enable,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Start of enable analyzer text span
                    enabledTextSpans.Add(new TextSpan(comment.SpanStart,
                        rootNode.FullSpan.End - comment.SpanStart + 1));
                }
                else if (commentText.Equals(AssignAllAnalyzer.CommentPattern_Disable,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // End of enable analyzer text span
                    TextSpan? currentEnabledTextSpan = enabledTextSpans.Cast<TextSpan?>().LastOrDefault();
                    if (currentEnabledTextSpan == null) continue;

                    int spanLength = comment.Span.Start - currentEnabledTextSpan.Value.Start;

                    // Update TextSpan in list
                    enabledTextSpans.RemoveAt(enabledTextSpans.Count - 1);
                    enabledTextSpans.Add(new TextSpan(currentEnabledTextSpan.Value.Start, spanLength));
                }
            }

            return new RegionsToAnalyze(enabledTextSpans.ToImmutableArray());
        }
    }
}