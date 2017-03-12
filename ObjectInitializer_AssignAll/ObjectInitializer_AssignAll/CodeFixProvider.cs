using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ObjectInitializer_AssignAll
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ObjectInitializer_AssignAllCodeFixProvider))]
    [Shared]
    public class ObjectInitializer_AssignAllCodeFixProvider : CodeFixProvider
    {
        public ObjectInitializer_AssignAllCodeFixProvider()
        {
            
        }
        private const string Title = "Assign all members";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ObjectInitializer_AssignAllAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            Diagnostic diagnostic = context.Diagnostics.First();
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            // Read unassigned member names, passed on from diagnostic
            string[] unassignedMemberNames = GetUnassignedMemberNames(diagnostic);
            if (!unassignedMemberNames.Any())
                return;

            // Find the object initializer identified by the diagnostic
            var objectInitializer = root.FindNode(diagnosticSpan) as InitializerExpressionSyntax;
            if (objectInitializer == null)
                return;

            // Register a code action that will invoke the fix
            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    ct => PopulateMissingAssignmentsAsync(context.Document, objectInitializer, unassignedMemberNames, ct),
                    Title),
                diagnostic);
        }

        private static async Task<Document> PopulateMissingAssignmentsAsync(Document document, InitializerExpressionSyntax objectInitializer, string[] unassignedMemberNames, CancellationToken ct)
        {
            // Can't manipulate syntax without a syntax root
            SyntaxNode oldRoot = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (oldRoot == null)
                return document;

            SeparatedSyntaxList<ExpressionSyntax> expressions = objectInitializer.Expressions;

            // Add missing member assignments in object initializer 
            SeparatedSyntaxList<ExpressionSyntax> newExpressions =
                expressions.AddRange(
                    unassignedMemberNames.Select(
                        memberName => SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName(memberName), SyntaxFactory.IdentifierName(string.Empty))));

            InitializerExpressionSyntax newObjectInitializer = objectInitializer.WithExpressions(
                newExpressions);

            // Reformat fails due to the codefix code not compiling..
//            newObjectInitializer =
//                (InitializerExpressionSyntax) Formatter.Format(newObjectInitializer, MSBuildWorkspace.Create());

            SyntaxNode newRoot = oldRoot.ReplaceNode(objectInitializer, newObjectInitializer);
            return document.WithSyntaxRoot(newRoot);
        }

        private static string[] GetUnassignedMemberNames(Diagnostic diagnostic)
        {
            string unassignedMemberNamesValue;
            if (!diagnostic.Properties.TryGetValue(ObjectInitializer_AssignAllAnalyzer.Properties_UnassignedMemberNames, out unassignedMemberNamesValue))
                return new string[0];

            return unassignedMemberNamesValue.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)
                .Select(str => str.Trim())
                .ToArray();
        }
    }
}