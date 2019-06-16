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
        public static int GetStableHashCode(params string [] texts)
        {
            unchecked
            {
                int hash = 23;
                foreach (string txt in texts)
                {
                    hash = hash * 31 + GetStableHashCode(txt);
                }
                return hash;
            }
        }
    }
}
