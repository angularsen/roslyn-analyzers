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
        internal const string Properties_UnassignedMemberNames = "UnassignedMemberNames";
        internal const string DiagnosticId = "ObjectInitializer_AssignAll";

        private const string CommentPattern_Disable = "ObjectInitializer_AssignAll disable";
        private const string CommentPattern_Enable = "ObjectInitializer_AssignAll enable";
        private const string CommentPattern_IgnoreProperties = "ObjectInitializer_AssignAll IgnoreProperties:";
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
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description,
            helpLinkUri: "https://github.com/anjdreas/roslyn-analyzers#objectinitializer_assignall");

        private ImmutableArray<TextSpan> _analyzerEnabledInTextSpans;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

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

            INamedTypeSymbol objectCreationNamedType = (INamedTypeSymbol) ctx.SemanticModel.GetSymbolInfo(objectCreation.Type).Symbol;
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
                string unassignedMembersString = String.Join(", ", unassignedMemberNames);

                var properties =
                    new Dictionary<string, string> {{Properties_UnassignedMemberNames, unassignedMembersString}}
                        .ToImmutableDictionary();

                Diagnostic diagnostic = Diagnostic.Create(Rule,
                    objectCreation.GetLocation(),
                    //ctx.Node.GetLocation(),
                    properties: properties,
                    messageArgs: new object[]
                    {
                        objectCreationNamedType.Name,
                        unassignedMembersString
                    });

                ctx.ReportDiagnostic(diagnostic);

            }
        }

        private static ImmutableArray<string> GetIgnoredPropertyNames(ObjectCreationExpressionSyntax objectCreation)
        {
            // Case 1: Comment before variable declaration and assignment:
            // <comment here>
            // Foo foo = new Foo { .. };
            if (new[] {SyntaxKind.EqualsValueClause, SyntaxKind.VariableDeclarator, SyntaxKind.VariableDeclaration}
                .SequenceEqual(objectCreation.Ancestors().Take(3).Select(x => x.Kind())))
            {
                VariableDeclarationSyntax variableDeclaration =
                    (VariableDeclarationSyntax) objectCreation.Ancestors().Skip(2).First();
                IdentifierNameSyntax identifierName =
                    variableDeclaration.ChildNodes().OfType<IdentifierNameSyntax>().First();

                SyntaxTrivia[] singleLineComments =
                    identifierName.Identifier.LeadingTrivia.Where(x => x.IsKind(SyntaxKind.SingleLineCommentTrivia))
                        .ToArray();

                return GetIgnoredPropertyNames(singleLineComments);
            }


            // Case 2: Comment before assignment (existing variable or member in an object initializer)
            // Foo foo;
            // <comment here>
            // foo = new Foo { .. };
            // Did not recognize syntax to locate leading comment for object initializer
            if (new[] {SyntaxKind.SimpleAssignmentExpression}
                .SequenceEqual(objectCreation.Ancestors().Take(1).Select(x => x.Kind())))
            {
                AssignmentExpressionSyntax assignmentExpression =
                    (AssignmentExpressionSyntax) objectCreation.Ancestors().First();
                IdentifierNameSyntax identifierName =
                    assignmentExpression.ChildNodes().OfType<IdentifierNameSyntax>().First();

                SyntaxTrivia[] singleLineComments =
                    identifierName.Identifier.LeadingTrivia.Where(x => x.IsKind(SyntaxKind.SingleLineCommentTrivia))
                        .ToArray();

                return GetIgnoredPropertyNames(singleLineComments);
            }

            // Did not recognize syntax to locate leading comment
            return ImmutableArray<string>.Empty;
        }

        private static ImmutableArray<string> GetIgnoredPropertyNames(SyntaxTrivia[] singleLineComments)
        {
            return singleLineComments.SelectMany(singleLineComment =>
            {
                string commentText = singleLineComment.ToString().Replace("//", "").Trim();
                if (commentText.StartsWith(CommentPattern_IgnoreProperties, StringComparison.OrdinalIgnoreCase))
                {
                    string ignorePropertiesText =
                        commentText.Substring(CommentPattern_IgnoreProperties.Length).Trim();

                    return
                        ignorePropertiesText.Split(new[] {", ", ","}, StringSplitOptions.RemoveEmptyEntries);
                }

                return Enumerable.Empty<string>();
            }).ToImmutableArray();
        }

        private bool IsAnalysisEnabledForSyntaxPosition(SyntaxNode initializer)
        {
            return _analyzerEnabledInTextSpans.Any(span => span.IntersectsWith(initializer.Span));
        }
    }
}