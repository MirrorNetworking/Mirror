namespace Mirror
{
    public static class StringHash
    {
        // string.GetHashCode is not guaranteed to be the same on all machines, but
        // we need one that is the same on all machines. simple and stupid:
        public static int GetStableHashCode(this string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        // calculate a stable hash out of several strings
        public static int GetStableHashCode(string txt1, string txt2, string txt3)
        {
            unchecked
            {
                int hash = 23;
                hash = hash * 31 + GetStableHashCode(txt1);
                hash = hash * 31 + GetStableHashCode(txt2);
                hash = hash * 31 + GetStableHashCode(txt3);
                return hash;
            }
        }
    }
}
