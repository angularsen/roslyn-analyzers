namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Roslyn enable analyzer ObjectInitializer_AssignAll
            Foo foo = new Foo
            {
                PropInt = 1,
                // Roslyn disable analyzer ObjectInitializer_AssignAll
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