using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = AssignAll.Test.Verifiers.CSharpCodeFixVerifier<
    AssignAll.AssignAllAnalyzer,
    AssignAll.AssignAllCodeFixProvider>;

namespace AssignAll.Test
{
    public class CodeFixTests
    {
        [Fact]
        public async Task EmptyInitializer_PopulatesAssignmentsForAllPublicMembers()
        {
            var testCode = @"
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
            Foo foo = {|#0:new Foo
            {
            }|#0};
        }
    }
}
";
            var fixedCode = @"
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
                FieldBool = ,
                PropInt = ,
                PropString = 
            };
        }
    }
}
";
            // Ignore compile errors in the fixed code, it is intentional to force user to fix it.
            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "FieldBool, PropInt, PropString");
            await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode, t => t.CompilerDiagnostics = CompilerDiagnostics.None);
        }

        [Fact]
        public async Task PopulatesMissingAssignmentsAfterExistingAssignments()
        {
            var testCode = @"
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
            Foo foo = {|#0:new Foo
            {
                PropInt = 1,
                // PropString not assigned
                FieldBool = true
            }|#0};
        }
    }
}
";
            var fixedCode = @"
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
            // NOTE: There seems to be added an unnecessary newline before the comma by the code fix, not sure how to fix that yet.
            // Ignore compile errors in the fixed code, it is intentional to force user to fix it.
            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropString");
            await VerifyCS.VerifyCodeFixAsync(testCode, expected, fixedCode, t => t.CompilerDiagnostics = CompilerDiagnostics.None);
        }
    }
}