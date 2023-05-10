namespace Mirror.Tests.Attributes
{
    public class ClassWithNoConstructor
    {
        public int a;
    }

    public class ClassWithConstructor
    {
        public int a;

        public ClassWithConstructor(int a)
        {
            this.a = a;
        }
    }
}
