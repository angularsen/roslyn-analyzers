// AssignAll enable
// EXAMPLE 006 - Exclude properties with private/protected setters and fields that are not accessible.
namespace Samples.ConsoleNet6;

internal static class Program
{
    private static void Main(string[] args)
    {
        // Gives analyzer error for all members except private setters and fields, since they are not accessible.
        // Missing member assignments in object initializer for type 'Foo'. Properties: PublicSetter, InternalSetter, ProtectedInternalSetter, PrivateSetter, PublicField, InternalField, ProtectedInternalField, PrivateField
        Foo foo = new()
        {
        };
    }

    private class Foo
    {
        public int PublicSetter { get; set; }
        public int InternalSetter { get; internal set; }
        public int ProtectedInternalSetter { get; protected internal set; }
        public int ProtectedSetter { get; protected set; }
        public int PrivateSetter { get; private set; }

        public int PublicField;
        internal int InternalField;
        protected internal int ProtectedInternalField;
        protected int ProtectedField;
        private int PrivateField;

        private Foo MethodWithAccessToPrivateMembers()
        {
            // Gives analyzer error for all members, including private setters and fields, since they are accessible.
            // Missing member assignments in object initializer for type 'Foo'. Properties: InternalField, InternalSetter, PrivateField, PrivateSetter, ProtectedInternalField, ProtectedInternalSetter, PublicSetter, PublicField
            return new Foo
            {
            };
        }
    }
}
