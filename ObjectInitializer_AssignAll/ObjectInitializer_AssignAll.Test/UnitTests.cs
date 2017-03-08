using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ObjectInitializer_AssignAll.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        //No diagnostics expected to show up
        [TestMethod]
        public void EmptyCode_ReturnsNoDiagnostics()
        {
            var test = @"";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void AllPropertiesAssigned_ReturnsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Foo foo = new Foo
            {
                PropInt = 1,
                PropString = ""2"",
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; }
        }
    }
}        
";

            VerifyCSharpDiagnostic(testContent);
        }

        [TestMethod]
        public void PropertyNotAssigned_PropertyFlaggedByDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var foo = new Foo
            {
                FieldBool = true,
                // Diagnostics should flag that this property is not set
                // PropInt = 1,
                PropString = ""my string""
            };

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
            public int PropIntReadOnly { get; }
            public bool FieldBool;
            public readonly bool FieldBoolReadOnly;
            public int this[int val] => val;

            public void MethodVoid() { }
            public int MethodInt() => 1;
        }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult();
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [TestMethod]
        public void ReadOnlyPropertyNotAssigned_Ok()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var foo = new Foo
            {
                // Cannot assign read-only property
                // PropIntReadOnly = 1,
            };

        private class Foo
        {
            public int PropIntReadOnly { get; }
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        [TestMethod]
        public void ReadOnlyFieldNotAssigned_Ok()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var foo = new Foo
            {
                // Cannot assign read-only field
                // FieldIntReadOnly = 1,
            };

        private class Foo
        {
            public readonly int PropIntReadOnly;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }


        [TestMethod]
        public void FieldNotAssigned_Error()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var foo = new Foo
            {
                // Not assigned, should give error
                // FieldInt = 1,
            };

        private class Foo
        {
            public int FieldInt;
        }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult();
            VerifyCSharpDiagnostic(testContent, expected);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ObjectInitializer_AssignAllAnalyzer();
        }

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult()
        {
            // Code snippets are identical up to the object initializer
            const int line = 9;
            const int column = 13;
            DiagnosticResult expected = new DiagnosticResult
            {
                Id = "ObjectInitializer_AssignAll",
                Message = "One or more properties/fields are not assigned in object initializer for type 'Foo'.",
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", line, column)
                    }
            };
            return expected;
        }
    }
}