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
        public void OnePropertyNotAssigned_PropertyFlaggedByDiagnostics()
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
            DiagnosticResult expected = new DiagnosticResult
            {
                Id = "ObjectInitializer_AssignAll",
                Message = "One or more properties/fields are not assigned in object initializer for type 'Foo'.",
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 9, 13)
                    }
            };

            VerifyCSharpDiagnostic(testContent, expected);
        }

//        //Diagnostic and CodeFix both triggered and checked for
//        [TestMethod]
//        public void TestMethod2()
//        {
//            var test = @"
//    using System;
//    using System.Collections.Generic;
//    using System.Linq;
//    using System.Text;
//    using System.Threading.Tasks;
//    using System.Diagnostics;
//
//    namespace ConsoleApplication1
//    {
//        class TypeName
//        {   
//        }
//    }";
//            DiagnosticResult expected = new DiagnosticResult
//            {
//                Id = "ObjectInitializer_AssignAll",
//                Message = string.Format("Type name '{0}' contains lowercase letters", "TypeName"),
//                Severity = DiagnosticSeverity.Warning,
//                Locations =
//                    new[]
//                    {
//                        new DiagnosticResultLocation("Test0.cs", 11, 15)
//                    }
//            };
//
//            VerifyCSharpDiagnostic(test, expected);
//
//            var fixtest = @"
//    using System;
//    using System.Collections.Generic;
//    using System.Linq;
//    using System.Text;
//    using System.Threading.Tasks;
//    using System.Diagnostics;
//
//    namespace ConsoleApplication1
//    {
//        class TYPENAME
//        {   
//        }
//    }";
//            VerifyCSharpFix(test, fixtest);
//        }

//        protected override CodeFixProvider GetCSharpCodeFixProvider()
//        {
//            return new ObjectInitializer_AssignAllCodeFixProvider();
//        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ObjectInitializer_AssignAllAnalyzer();
        }
    }
}