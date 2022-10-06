// AssignAll enable
// EXAMPLE 004 - Analyzer should also consider public members from any base types.
namespace Samples.ConsoleNet6;

public static class Example004_Inheritance
{
    public static void Irrelevant()
    {
        // This should give analyzer error:
        // Missing member assignments in object initializer for type 'Derived'. Properties: BasePropUnassigned, DerivedPropUnassigned
        var foo = new Derived
        {
            // Commented assignments after opening brace.
            // BasePropCommented = ,
            // DerivedPropCommented = ,

            // Assigned property, OK by analyzer
            BasePropAssigned = 1,
            DerivedPropAssigned = 1,
        };
    }

    private class Base
    {
        public int BasePropAssigned { get; set; }
        public int BasePropCommented { get; set; }
        public int BasePropUnassigned { get; set; }
    }

    private class Derived : Base
    {
        public int DerivedPropAssigned { get; set; }
        public int DerivedPropCommented { get; set; }
        public int DerivedPropUnassigned { get; set; }
    }
}
