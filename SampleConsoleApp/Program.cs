namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main()
        {
            // AssignAll enable
            var foo = new Foo
            {
                // Commented assignments after opening brace.
                // PropCommented1 = 1,

                // Assigned property, OK'ed by analyzer
                PropAssigned = 1,

                // Commented assignments just before closing brace
                //PropCommented2 = ,
                // PropCommented3=,
            };
        }

        private class Foo
        {
            public int PropAssigned { get; set; }
            public int PropCommented1 { get; set; }
            public int PropCommented2 { get; set; }
            public int PropCommented3 { get; set; }
            public int PropUnassigned { get; set; }
        }
    }
}