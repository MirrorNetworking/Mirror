namespace Mirror
{
    public static class Extensions
    {
        // Copy of Mono string.GetHashCode(), so that we generate same hashes regardless of runtime (mono/MS .NET)
        public static int GetStableHashCode(this string s)
        {
            unsafe
            {
                int length = s.Length;
                fixed(char* c = s)
                {
                    char* cc = c;
                    char* end = cc + length - 1;
                    int h = 0;
                    for (; cc < end; cc += 2)
                    {
                        h = (h << 5) - h + *cc;
                        h = (h << 5) - h + cc[1];
                    }
                    ++end;
                    if (cc < end)
                        h = (h << 5) - h + *cc;
                    return h;
                }
            }
        }

        public static int GetStableHashCodeOLD(this string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;












            }
        }
    }
}