using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AssignAll2
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssignAll2CodeFixProvider)), Shared]
    public class AssignAll2CodeFixProvider : CodeFixProvider
    {
        private const string Title = "Assign all members";
        private const string CodeFixUniqueId = "Assign all members";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AssignAll2Analyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root =
                await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            Diagnostic diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
                return;
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            // Read unassigned member names, passed on from diagnostic
            string[] unassignedMemberNames = GetUnassignedMemberNames(diagnostic);
            if (!unassignedMemberNames.Any())
                return;

            // Find the object initializer identified by the diagnostic
            ObjectCreationExpressionSyntax objectCreation =
                root.FindNode(diagnosticSpan) as ObjectCreationExpressionSyntax;
            InitializerExpressionSyntax objectInitializer = objectCreation?.Initializer;
            if (objectInitializer == null)
                return;

            // Register a code action that will invoke the fix
            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    ct =>
                        PopulateMissingAssignmentsAsync(context.Document, objectInitializer, unassignedMemberNames,
                            ct),
                    CodeFixUniqueId),
                diagnostic);
        }

        private static async Task<Document> PopulateMissingAssignmentsAsync(Document document,
            InitializerExpressionSyntax objectInitializer, string[] unassignedMemberNames, CancellationToken ct)
        {
            // Can't manipulate syntax without a syntax root
            SyntaxNode oldRoot = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (oldRoot == null)
                return document;

            SeparatedSyntaxList<ExpressionSyntax> expressions = objectInitializer.Expressions;
            
            // Add missing member assignments in object initializer.
            // End of line honors .editorconfig and/or system preferences, but it does NOT honor if a different EOL used in the file.
            SeparatedSyntaxList<ExpressionSyntax> newExpressions = expressions
                .AddRange(unassignedMemberNames.Select(CreateEmptyMemberAssignmentExpression));

            InitializerExpressionSyntax newObjectInitializer = objectInitializer
                .WithExpressions(newExpressions);

            SyntaxNode newRoot = oldRoot.ReplaceNode(objectInitializer, newObjectInitializer);
            return document.WithSyntaxRoot(newRoot);
        }

       private static AssignmentExpressionSyntax CreateEmptyMemberAssignmentExpression(string memberName) => SyntaxFactory
            .AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(memberName), SyntaxFactory.IdentifierName(string.Empty));

       private static string[] GetUnassignedMemberNames(Diagnostic diagnostic)
        {
            if (!diagnostic.Properties.TryGetValue(
                    AssignAll2Analyzer.Properties_UnassignedMemberNames, out string unassignedMemberNamesValue))
                return new string[0];

            return unassignedMemberNamesValue.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(str => str.Trim())
                .ToArray();
        }
    }
}
