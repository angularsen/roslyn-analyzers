using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ObjectInitializer_AssignAll
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ObjectInitializer_AssignAllAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = "ObjectInitializer_AssignAll";
        private const string Category = "Naming";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager,
                typeof(Resources));

        private static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager,
                typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat,
            Category, DiagnosticSeverity.Error, true, Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext ctx)
        {
            ctx.RegisterCodeBlockStartAction<SyntaxKind>(startCodeBlockContext =>
            {

                var analyzer = new UnassignedMemberAnalyzer(startCodeBlockContext);

            });
        }

        private class UnassignedMemberAnalyzer
        {
            private const string DisableAnalyzerCommentPattern = "Roslyn disable analyzer ObjectInitializer_AssignAll";
            private const string EnableAnalyzerCommentPattern = "Roslyn enable analyzer ObjectInitializer_AssignAll";
            private const string IgnorePropertiesAnalyzerCommentPattern = "Roslyn ObjectInitializer_AssignAll IgnoreProperties:";
            private readonly ImmutableArray<TextSpan> _analyzerEnabledInTextSpans;
            private readonly ImmutableArray<string> _ignoredPropertyNames = ImmutableArray<string>.Empty;

            public UnassignedMemberAnalyzer(CodeBlockStartAnalysisContext<SyntaxKind> startCodeBlockContext)
            {
                var singleLineComments = startCodeBlockContext.CodeBlock.DescendantTrivia()
                    .Where(x => x.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    .OrderBy(x => x.SpanStart);

                var enabledTextSpans = new List<TextSpan>();
                foreach (SyntaxTrivia comment in singleLineComments)
                {
                    string commentText = comment.ToString().Replace("//", "").Trim();
                    if (commentText.Equals(EnableAnalyzerCommentPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        // Start of enable analyzer text span
                        enabledTextSpans.Add(new TextSpan(comment.SpanStart,
                            startCodeBlockContext.CodeBlock.Span.End - comment.SpanStart));
                    }
                    else if (commentText.Equals(DisableAnalyzerCommentPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        // End of enable analyzer text span
                        TextSpan? currentEnabledTextSpan = enabledTextSpans.Cast<TextSpan?>().LastOrDefault();
                        if (currentEnabledTextSpan == null) continue;

                        int spanLength = comment.Span.Start - currentEnabledTextSpan.Value.Start;

                        // Update TextSpan in list
                        enabledTextSpans.RemoveAt(enabledTextSpans.Count - 1);
                        enabledTextSpans.Add(new TextSpan(currentEnabledTextSpan.Value.Start, spanLength));
                    }
                    else if (commentText.StartsWith(IgnorePropertiesAnalyzerCommentPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        string ignorePropertiesText =
                            commentText.Substring(IgnorePropertiesAnalyzerCommentPattern.Length).Trim();

                        _ignoredPropertyNames = ignorePropertiesText
                            .Split(new[] {", ", ","},
                                StringSplitOptions.RemoveEmptyEntries)
                            .ToImmutableArray();
                    }
                }

                _analyzerEnabledInTextSpans = enabledTextSpans.ToImmutableArray();

                startCodeBlockContext.RegisterSyntaxNodeAction(AnalyzeObjectInitializer,
                    SyntaxKind.ObjectInitializerExpression);

                // Register an end action to report diagnostics based on the final state.
                startCodeBlockContext.RegisterCodeBlockEndAction(CodeBlockEndAction);
            }

            private void CodeBlockEndAction(CodeBlockAnalysisContext ctx)
            {
                // Keep-alive instance from garbage collector
            }

            private void AnalyzeObjectInitializer(SyntaxNodeAnalysisContext ctx)
            {
                InitializerExpressionSyntax objectInitializer = (InitializerExpressionSyntax)ctx.Node;

                // Should be direct parent of ObjectInitializerExpression
                var objectCreation = objectInitializer.Parent as ObjectCreationExpressionSyntax;

                // Only handle initializers immediately following object creation,
                // not sure what the scenario would be since we are only registered for
                // object initializers, not things like list/collection initializers.
                if (objectCreation == null)
                    return;

                // For now, only perform analysis when explicitly enabled by comment.
                // TODO Support other means to enable, such as static configuration (analyze all/none by default), attributes on types and members
                bool isEnabledByComment = IsAnalysisEnabledForSyntaxPosition(objectCreation);
                if (!isEnabledByComment)
                    return;

                SymbolInfo symbolInfo = ctx.SemanticModel.GetSymbolInfo(objectCreation.Type);

                ImmutableArray<ISymbol> members = ((INamedTypeSymbol)symbolInfo.Symbol).GetMembers();

                List<string> assignedMemberNames = objectInitializer.ChildNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Select(assignmentSyntax => ((IdentifierNameSyntax)assignmentSyntax.Left).Identifier.ValueText)
                    .ToList();


                // TODO Check if member is assignable using Roslyn data flow analysis instead of these constraints,
                // as that is the only way to properly determine if it is assignable or not in a context
                IEnumerable<ISymbol> assignableProperties = members
                    .OfType<IPropertySymbol>()
                    .Where(m =>
                            // Exclude indexer properties
                            !m.IsIndexer &&
                            // Exclude read-only getter properties
                            !m.IsReadOnly &&
                            // Simplification, only care about public members
                            m.DeclaredAccessibility == Accessibility.Public);

                IEnumerable<ISymbol> assignableFields = members.OfType<IFieldSymbol>()
                    .Where(m =>
                            // Exclude readonly fields
                            !m.IsReadOnly &&
                            // Exclude const fields
                            !m.HasConstantValue &&
                            // Exclude generated backing fields for properties
                            !m.IsImplicitlyDeclared &&
                            // Simplification, only care about public members
                            m.DeclaredAccessibility == Accessibility.Public);

                IEnumerable<string> assignableMemberNames = assignableProperties
                    .Concat(assignableFields)
                    .Select(x => x.Name);

                List<string> unassignedMemberNames =
                    assignableMemberNames
                        .Except(assignedMemberNames)
                        .Except(_ignoredPropertyNames)
                        .ToList();

                if (unassignedMemberNames.Any())
                {
                    string unassignedMembersString = string.Join(", ", unassignedMemberNames);

                    Diagnostic diagnostic = Diagnostic.Create(Rule, ctx.Node.GetLocation(), symbolInfo.Symbol.Name,
                        unassignedMembersString);
                    ctx.ReportDiagnostic(diagnostic);
                }
            }

            private bool IsAnalysisEnabledForSyntaxPosition(ObjectCreationExpressionSyntax objectCreation)
            {
                return _analyzerEnabledInTextSpans.Any(span => span.IntersectsWith(objectCreation.Span));
            }
       }
    }
}