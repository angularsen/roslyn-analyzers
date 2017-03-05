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
//                Prop2 = "2",
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