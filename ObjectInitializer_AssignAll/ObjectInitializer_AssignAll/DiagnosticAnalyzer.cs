using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
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
        internal const string Properties_UnassignedMemberNames = "UnassignedMemberNames";
        internal const string DiagnosticId = "ObjectInitializer_AssignAll";

        private const string CommentPattern_Disable = "ObjectInitializer_AssignAll disable";
        private const string CommentPattern_Enable = "ObjectInitializer_AssignAll enable";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager,
                typeof(Resources));

        private static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager,
                typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat,
            Category, DiagnosticSeverity.Error, true, Description,
            "https://github.com/anjdreas/roslyn-analyzers#objectinitializer_assignall");

        private ImmutableArray<TextSpan> _analyzerEnabledInTextSpans;

        /// <summary>
        ///     Regex that identifies:
        ///     Group 1: Name of property/field in commented assignment
        /// </summary>
        private static readonly Regex CommentedMemberAssignmentRegex = new Regex(@"\/\/\s*(\w+)\s*=");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule)
            ;

        public override void Initialize(AnalysisContext ctx)
        {
            ctx.RegisterSyntaxTreeAction(AnalyzeComments);
            ctx.RegisterSyntaxNodeAction(AnalyzeObjectInitializers, SyntaxKind.ObjectInitializerExpression);
        }

        private void AnalyzeComments(SyntaxTreeAnalysisContext syntaxTreeContext)
        {
            SyntaxNode root = syntaxTreeContext.Tree.GetCompilationUnitRoot(syntaxTreeContext.CancellationToken);

            IOrderedEnumerable<SyntaxTrivia> singleLineComments =
                root.DescendantTrivia()
                    .Where(x => x.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    .OrderBy(x => x.SpanStart);

            var enabledTextSpans = new List<TextSpan>();
            foreach (SyntaxTrivia comment in singleLineComments)
            {
                string commentText = comment.ToString().Replace("//", "").Trim();
                if (commentText.Equals(CommentPattern_Enable, StringComparison.OrdinalIgnoreCase))
                {
                    // Start of enable analyzer text span
                    enabledTextSpans.Add(new TextSpan(comment.SpanStart, root.FullSpan.End - comment.SpanStart));
                }
                else if (commentText.Equals(CommentPattern_Disable, StringComparison.OrdinalIgnoreCase))
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

            _analyzerEnabledInTextSpans = enabledTextSpans.ToImmutableArray();
        }

        private void AnalyzeObjectInitializers(SyntaxNodeAnalysisContext ctx)
        {
            InitializerExpressionSyntax objectInitializer = (InitializerExpressionSyntax) ctx.Node;

            // For now, only perform analysis when explicitly enabled by comment.
            // TODO Support other means to enable, such as static configuration (analyze all/none by default), attributes on types and members
            if (!IsAnalysisEnabledForSyntaxPosition(objectInitializer)) return;

            // Should be direct parent of ObjectInitializerExpression
            ObjectCreationExpressionSyntax objectCreation =
                objectInitializer.Parent as ObjectCreationExpressionSyntax;

            // Only handle initializers immediately following object creation,
            // not sure what the scenario would be since we are only registered for
            // object initializers, not things like list/collection initializers.
            if (objectCreation == null)
                return;

            INamedTypeSymbol objectCreationNamedType =
                (INamedTypeSymbol) ctx.SemanticModel.GetSymbolInfo(objectCreation.Type).Symbol;
            if (objectCreationNamedType == null)
                return;

            ImmutableArray<ISymbol> members = objectCreationNamedType.GetMembers();

            List<string> assignedMemberNames = objectInitializer.ChildNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Select(assignmentSyntax => ((IdentifierNameSyntax) assignmentSyntax.Left).Identifier.ValueText)
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

            ImmutableArray<string> ignoredPropertyNames = GetIgnoredPropertyNames(objectCreation);

            List<string> unassignedMemberNames =
                assignableMemberNames
                    .Except(assignedMemberNames)
                    .Except(ignoredPropertyNames)
                    .ToList();

            if (unassignedMemberNames.Any())
            {
                string unassignedMembersString = string.Join(", ", unassignedMemberNames);

                ImmutableDictionary<string, string> properties =
                    new Dictionary<string, string> {{Properties_UnassignedMemberNames, unassignedMembersString}}
                        .ToImmutableDictionary();

                Diagnostic diagnostic = Diagnostic.Create(Rule,
                    objectCreation.GetLocation(),
                    //ctx.Node.GetLocation(),
                    properties, objectCreationNamedType.Name, unassignedMembersString);

                ctx.ReportDiagnostic(diagnostic);
            }
        }

        private static ImmutableArray<string> GetIgnoredPropertyNames(ObjectCreationExpressionSyntax objectCreation)
        {
            ImmutableArray<string> propertiesByCommentedAssignment = GetIgnoredPropertyNamesFromCommentedAssignments(objectCreation);
            return propertiesByCommentedAssignment;
        }

        private static ImmutableArray<string> GetIgnoredPropertyNamesFromCommentedAssignments(ObjectCreationExpressionSyntax objectCreation)
        {
            // Case 1: Commented member assignments before one or more actual member assignments
            // return new Foo {
            //   // Prop1 = null,
            //   // Prop2 = null,
            //   Prop3 = 1
            // };
            SyntaxTriviaList memberAssignmentsLeadingTrivia =
                new SyntaxTriviaList().AddRange(objectCreation.Initializer.Expressions
                    .OfType<AssignmentExpressionSyntax>()
                    .SelectMany(e => e.Left.GetLeadingTrivia()));

            // Case 2: Commented member assignments before closing brace
            // return new Foo {
            //   Prop3 = 1
            //   // Prop1 = null,
            //   // Prop2 = null,
            // };
            SyntaxTriviaList closingBraceLeadingTrivia = objectCreation.Initializer.CloseBraceToken.LeadingTrivia;

            return
                memberAssignmentsLeadingTrivia
                    .Concat(closingBraceLeadingTrivia)
                    .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    .Select(trivia => CommentedMemberAssignmentRegex.Match(trivia.ToString()))
                    .Where(match => match.Success)
                    .Select(match => match.Groups[1].Value)
                    .ToImmutableArray();
        }

        private bool IsAnalysisEnabledForSyntaxPosition(SyntaxNode initializer)
        {
            return _analyzerEnabledInTextSpans.Any(span => span.IntersectsWith(initializer.Span));
        }
    }
}