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
        public void PropertiesNotAssigned_PropertyNamesIncludedInDiagnostics()
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
                // Diagnostics should flag that these properties are not set
                // PropInt = 1,
                // PropString = ""my string""
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
        }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("PropInt", "PropString");
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [TestMethod]
        public void IndexerPropertyNotAssigned_Ok()
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
            };
        }

        private class Foo
        {
            public int this[int val] => val;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        [TestMethod]
        public void MethodsNotAssigned_Ok()
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
            };
        }

        private class Foo
        {
            public void MethodVoid() { }
            public int MethodInt() => 1;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
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
        }

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
        }

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
        }

        private class Foo
        {
            public int FieldInt;
        }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("FieldInt");
            VerifyCSharpDiagnostic(testContent, expected);
        }

        /// <remarks>TODO Revisit this when the implementation supports looking at context and whether the member can be assigned or not.</remarks>
        [TestMethod]
        public void NonPublicFieldsNotAssigned_Ok()
        {
            string[] accessModifiers = {"private", "internal", "protected", "protected internal"};
            foreach (string accessModifier in accessModifiers)
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
                // The implementation is currently limited to public only, so all other access modifiers will be ignored
                // FieldInt = 1,
            };
        }

        private class Foo
        {
            {{accessModifier}} int FieldInt;
        }
    }
}
".Replace("{{accessModifier}}", accessModifier);

                VerifyCSharpDiagnostic(testContent);
            }
        }

        [TestMethod]
        public void UnassignedMembersWithoutObjectInitializer_Ok()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Unassigned properties and fields are ignored for this type of construction
            var foo = new Foo();
            // foo.FieldInt = 1;
        }

        private class Foo
        {
            public int FieldInt;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ObjectInitializer_AssignAllAnalyzer();
        }

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult(params string[] unassignedMemberNames)
        {
            // Code snippets are identical up to the object initializer
            const int line = 9;
            const int column = 13;
            string unassignedMembersString = string.Join(", ", unassignedMemberNames);
            DiagnosticResult expected = new DiagnosticResult
            {
                Id = "ObjectInitializer_AssignAll",
                Message = $"Missing assignment for members of type 'Foo': {unassignedMembersString}",
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