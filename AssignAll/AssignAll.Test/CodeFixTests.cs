using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using Xunit;

namespace AssignAll.Test
{
    public class CodeFixTests : CodeFixVerifier
    {
        [Fact]
        public void EmptyInitializer_PopulatesAssignmentsForAllPublicMembers()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
            public bool FieldBool;
        }

        private static void Main(string[] args)
        {
            // AssignAll enable
            Foo foo = new Foo
            {
            };
        }
    }
}
";
            var fixedContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
            public bool FieldBool;
        }

        private static void Main(string[] args)
        {
            // AssignAll enable
            Foo foo = new Foo
            {
                PropInt = ,
                PropString = ,
                FieldBool = 
            };
        }
    }
}
";
            // The code fix will produce intentional compile errors,
            // so skip new compiler diagnostics as they will just fail
            VerifyCSharpFix(testContent, fixedContent, allowNewCompilerDiagnostics: true);
        }

        [Fact]
        public void PopulatesMissingAssignmentsAfterExistingAssignments()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
            public bool FieldBool;
        }

        private static void Main(string[] args)
        {
            // AssignAll enable
            Foo foo = new Foo
            {
                PropInt = 1,
                // PropString not assigned
                FieldBool = true
            };
        }
    }
}
";
            var fixedContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
            public bool FieldBool;
        }

        private static void Main(string[] args)
        {
            // AssignAll enable
            Foo foo = new Foo
            {
                PropInt = 1,
                // PropString not assigned
                FieldBool = true
,
                PropString = 
            };
        }
    }
}
";
            // NOTE: There seems to be added an unnecessary newline before the comma by the code fix, not sure how to fix that yet
            // The code fix will produce intentional compile errors,
            // so skip new compiler diagnostics as they will just fail
            VerifyCSharpFix(testContent, fixedContent, allowNewCompilerDiagnostics: true);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AssignAllAnalyzer();
        }

        protected override Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new CodeFixProvider();
        }

    }
}