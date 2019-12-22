using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace AssignAll.AssignAllMembers
{
    internal struct RegionsToAnalyze
    {
        public RegionsToAnalyze(ImmutableArray<TextSpan> textSpans)
        {
            TextSpans = textSpans;
            Created = DateTimeOffset.Now;
        }

        public ImmutableArray<TextSpan> TextSpans { get; }
        public DateTimeOffset Created { get; }
    }
}