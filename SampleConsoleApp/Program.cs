namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // ObjectInitializer_AssignAll enable
            Foo foo = new Foo
            {
                PropInt = 1,
                // ObjectInitializer_AssignAll disable
                Bar = new Bar
                {
                    //PropInt = 2
                }
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public Bar Bar { get; internal set; }
        }

        private class Bar
        {
            public int PropInt { get; set; }
        }
    }
}