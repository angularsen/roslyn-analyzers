using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
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
                Prop1 = 1,
                Prop2 = ""2"",
                Field3 = true
            };
        }

        private class Foo
        {
            public int Prop1 { get; set; }
            public string Prop2 { get; set; }
            public bool Field3 { get; set; }
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
            Foo foo = new Foo
            {
                Prop1 = 1,
                // Diagnostics should flag that this property is not set
//                Prop2 = ""2"",
                Field3 = true
            };
        }

        private class Foo
        {
            public int Prop1 { get; set; }
            public string Prop2 { get; set; }
            public bool Field3 { get; set; }
        }
    }
}        
";
            DiagnosticResult expected = new DiagnosticResult
            {
                Id = "ObjectInitializer_AssignAll",
                Message = "Type name 'TypeName' contains lowercase letters",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 11, 15)
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

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ObjectInitializer_AssignAllCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ObjectInitializer_AssignAllAnalyzer();
        }
    }
}