// safety fuse for weaver to flip.
// runtime can check this to ensure weaving succeded.
// otherwise running server/client would give lots of random 'writer not found' etc. errors.
// this is much cleaner.
namespace Mirror
{
    public static class WeaverFuse
    {
        public static bool State => false;
    }
}
