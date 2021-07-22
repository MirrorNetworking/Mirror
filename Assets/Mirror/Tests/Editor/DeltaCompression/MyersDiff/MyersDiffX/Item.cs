namespace MyersDiffX
{
    // details of one difference.
    public struct Item
    {
        // Start Line number in Data A.
        public int StartA;
        // Start Line number in Data B.
        public int StartB;

        // delete 'n' from A at StartA.
        public int deletedA;
        // insert 'n' from B [StartB .. StartB+N] after A at StartA.
        public int insertedB;

        public Item(int StartA, int StartB, int deletedA, int insertedB)
        {
            this.StartA = StartA;
            this.StartB = StartB;
            this.deletedA = deletedA;
            this.insertedB = insertedB;
        }
    }
}