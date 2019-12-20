using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssignAll.Test.Verifiers
{
    /// <summary>
    ///     Superclass of all Unit tests made for diagnostics with codefixes.
    ///     Contains methods used to verify correctness of codefixes
    /// </summary>
    public abstract partial class CodeFixVerifier : DiagnosticVerifier
    {
        /// <summary>
        ///     Returns the codefix being tested (C#) - to be implemented in non-abstract class
        /// </summary>
        /// <returns>The CodeFixProvider to be used for CSharp code</returns>
        protected virtual CodeFixProvider GetCSharpCodeFixProvider()
        {
            return null;
        }

        /// <summary>
        ///     Returns the codefix being tested (VB) - to be implemented in non-abstract class
        /// </summary>
        /// <returns>The CodeFixProvider to be used for VisualBasic code</returns>
        protected virtual CodeFixProvider GetBasicCodeFixProvider()
        {
            return null;
        }

        /// <summary>
        ///     Called to test a C# codefix when applied on the inputted string as a source
        /// </summary>
        /// <param name="oldSource">A class in the form of a string before the CodeFix was applied to it</param>
        /// <param name="newSource">A class in the form of a string after the CodeFix was applied to it</param>
        /// <param name="verifyDiagnosticsRemovedByCodeFix"></param>
        /// <param name="codeFixIndex">Index determining which codefix to apply if there are multiple</param>
        /// <param name="allowNewCompilerDiagnostics">
        ///     A bool controlling whether or not the test will fail if the CodeFix
        ///     introduces other warnings after being applied
        /// </param>
        protected void VerifyCSharpFix(string oldSource, string newSource, int? codeFixIndex = null,
            bool allowNewCompilerDiagnostics = false, bool verifyDiagnosticsRemovedByCodeFix = true)
        {
            VerifyFix(LanguageNames.CSharp, GetCSharpDiagnosticAnalyzer(), GetCSharpCodeFixProvider(), oldSource,
                newSource, codeFixIndex, allowNewCompilerDiagnostics, verifyDiagnosticsRemovedByCodeFix);
        }

        /// <summary>
        ///     Called to test a VB codefix when applied on the inputted string as a source
        /// </summary>
        /// <param name="oldSource">A class in the form of a string before the CodeFix was applied to it</param>
        /// <param name="newSource">A class in the form of a string after the CodeFix was applied to it</param>
        /// <param name="codeFixIndex">Index determining which codefix to apply if there are multiple</param>
        /// <param name="allowNewCompilerDiagnostics">
        ///     A bool controlling whether or not the test will fail if the CodeFix
        ///     introduces other warnings after being applied
        /// </param>
        /// <param name="verifyDiagnosticsRemovedByCodeFix">Whether to verify diagnostics are removed by code fix.</param>
        protected void VerifyBasicFix(string oldSource, string newSource, int? codeFixIndex = null,
            bool allowNewCompilerDiagnostics = false, bool verifyDiagnosticsRemovedByCodeFix = true)
        {
            VerifyFix(LanguageNames.VisualBasic, GetBasicDiagnosticAnalyzer(), GetBasicCodeFixProvider(), oldSource,
                newSource, codeFixIndex, allowNewCompilerDiagnostics, verifyDiagnosticsRemovedByCodeFix);
        }

        /// <summary>
        ///     General verifier for codefixes.
        ///     Creates a Document from the source string, then gets diagnostics on it and applies the relevant codefixes.
        ///     Then gets the string after the codefix is applied and compares it with the expected result.
        ///     Note: If any codefix causes new diagnostics to show up, the test fails unless allowNewCompilerDiagnostics is set to
        ///     true.
        /// </summary>
        /// <param name="language">The language the source code is in</param>
        /// <param name="analyzer">The analyzer to be applied to the source code</param>
        /// <param name="codeFixProvider">The codefix to be applied to the code wherever the relevant Diagnostic is found</param>
        /// <param name="oldSource">A class in the form of a string before the CodeFix was applied to it</param>
        /// <param name="newSource">A class in the form of a string after the CodeFix was applied to it</param>
        /// <param name="codeFixIndex">Index determining which codefix to apply if there are multiple</param>
        /// <param name="allowNewCompilerDiagnostics">
        ///     A bool controlling whether or not the test will fail if the CodeFix
        ///     introduces other warnings after being applied
        /// </param>
        /// <param name="verifyDiagnosticsRemovedByCodeFix">
        ///     Set to true to verify all diagnostics are removed by the code fix. Set to
        ///     false to not create diagnostics at all after the code fix, which is useful if the codefix intentionally creates
        ///     compile errors.
        /// </param>
        private void VerifyFix(string language, DiagnosticAnalyzer analyzer, CodeFixProvider codeFixProvider,
            string oldSource, string newSource, int? codeFixIndex, bool allowNewCompilerDiagnostics,
            bool verifyDiagnosticsRemovedByCodeFix)
        {
            Document document = CreateDocument(oldSource, language);
            Diagnostic[] analyzerDiagnostics = GetSortedDiagnosticsFromDocuments(analyzer, new[] {document});
            IList<Diagnostic> compilerDiagnostics = CodeFixVerifier.GetCompilerDiagnostics(document).ToList();
            int attempts = analyzerDiagnostics.Length;

            for (var i = 0; i < attempts; ++i)
            {
                var actions = new List<CodeAction>();
                CodeFixContext context = new CodeFixContext(document, analyzerDiagnostics[0], (a, d) => actions.Add(a),
                    CancellationToken.None);
                codeFixProvider.RegisterCodeFixesAsync(context).Wait();

                if (!actions.Any())
                    break;

                if (codeFixIndex != null)
                {
                    document = CodeFixVerifier.ApplyFix(document, actions.ElementAt((int) codeFixIndex));
                    break;
                }

                document = CodeFixVerifier.ApplyFix(document, actions.ElementAt(0));
                if (verifyDiagnosticsRemovedByCodeFix)
                {
                    Diagnostic[] analyzerDiagnosticsAfterCodeFix = GetSortedDiagnosticsFromDocuments(analyzer,
                        new[] {document});

                    IEnumerable<Diagnostic> newCompilerDiagnostics = CodeFixVerifier.GetNewDiagnostics(compilerDiagnostics,
                        CodeFixVerifier.GetCompilerDiagnostics(document));

                    //check if applying the code fix introduced any new compiler diagnostics
                    if (!allowNewCompilerDiagnostics && newCompilerDiagnostics.Any())
                    {
                        // Format and get the compiler diagnostics again so that the locations make sense in the output
                        document =
                            document.WithSyntaxRoot(Formatter.Format(document.GetSyntaxRootAsync().Result,
                                Formatter.Annotation, document.Project.Solution.Workspace));
                        newCompilerDiagnostics = CodeFixVerifier.GetNewDiagnostics(compilerDiagnostics, CodeFixVerifier.GetCompilerDiagnostics(document));

                        Assert.IsTrue(false,
                            $@"Fix introduced new compiler diagnostics:
{string.Join("\r\n", newCompilerDiagnostics.Select(d => d.ToString()))}

New document:
{document.GetSyntaxRootAsync().Result.ToFullString()}
");
                    }

                    //check if there are analyzer diagnostics left after the code fix
                    if (!analyzerDiagnosticsAfterCodeFix.Any())
                        break;
                }
            }

            //after applying all of the code fixes, compare the resulting string to the inputted one
            string actual = CodeFixVerifier.GetStringFromDocument(document);
            Debug.WriteLine($"Actual bytes: [{GetHexValues(actual)}]");
            Debug.WriteLine($"Expected bytes: [{GetHexValues(actual)}]");
            Assert.AreEqual(newSource, actual);
        }

        private static string GetHexValues(string actual)
        {
            var bytes = Encoding.UTF8.GetBytes(actual);
            return $"Bytes ({bytes.Length}): {string.Join(",", bytes.Select(ch => ch.ToString("X")))}";
        }
    }
}