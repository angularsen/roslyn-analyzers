using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using AssignAll.AssignAllMembers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AssignAll
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AssignAllAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "AssignAll";
        internal const string Properties_UnassignedMemberNames = "UnassignedMemberNames";
        private const int MaxRootNodeCacheCount = 10;
        private const string Category = "Usage";

        /// <summary>
        ///     Regex that identifies:
        ///     Group 1: Name of property/field in commented assignment
        /// </summary>
        internal static readonly Regex CommentedMemberAssignmentRegex = new Regex(@"\/\/\s*(\w+)\s*=");


        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title =
            new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

        internal static DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, true, Description);

        private IImmutableDictionary<SyntaxReference, RegionsToAnalyze> _rootNodeToAnalyzerTextSpans =
            ImmutableDictionary<SyntaxReference, RegionsToAnalyze>.Empty;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext ctx)
        {
            ctx.EnableConcurrentExecution();
            ctx.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None); // Don't touch code classified as generated
            ctx.RegisterCodeBlockStartAction<SyntaxKind>(RegisterObjectInitializerAnalyzerOnCodeBlockStart);
        }

        private void RegisterObjectInitializerAnalyzerOnCodeBlockStart(CodeBlockStartAnalysisContext<SyntaxKind> block)
        {
            RegionsToAnalyze regionsToAnalyze = GetOrSetCachedRegionsToAnalyzeInFile(block.CodeBlock);

            var objectInitializerAnalyzer = new ObjectInitializerAnalyzer(regionsToAnalyze);
            block.RegisterSyntaxNodeAction(objectInitializerAnalyzer.AnalyzeObjectInitializers, SyntaxKind.ObjectInitializerExpression);
        }

        private RegionsToAnalyze GetOrSetCachedRegionsToAnalyzeInFile(SyntaxNode codeBlock)
        {
            SyntaxNode rootNode = codeBlock.Ancestors().LastOrDefault() ?? codeBlock;
            SyntaxReference rootNodeRef = rootNode.GetReference();

            if (_rootNodeToAnalyzerTextSpans.TryGetValue(rootNodeRef, out RegionsToAnalyze regionsToAnalyze))
                return regionsToAnalyze;

            regionsToAnalyze = RegionsToAnalyzeProvider.GetRegionsToAnalyze(rootNode);
            _rootNodeToAnalyzerTextSpans = _rootNodeToAnalyzerTextSpans.SetItem(rootNodeRef, regionsToAnalyze);
            Debug.WriteLine("Cache root node: " + rootNode);

            RemoveExcessCache();
            return regionsToAnalyze;
        }

        private void RemoveExcessCache()
        {
            while (_rootNodeToAnalyzerTextSpans.Count > MaxRootNodeCacheCount)
            {
                SyntaxReference oldestCacheKey = _rootNodeToAnalyzerTextSpans.OrderBy(x => x.Value.Created)
                    .Last().Key;
                _rootNodeToAnalyzerTextSpans = _rootNodeToAnalyzerTextSpans.Remove(oldestCacheKey);
                Debug.WriteLine("Remove cached item: " + oldestCacheKey);
            }
        }
    }
}