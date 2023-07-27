// safety fuse for weaver to flip.
// runtime can check this to ensure weaving succeded.
// otherwise running server/client would give lots of random 'writer not found' etc. errors.
// this is much cleaner.
//
// note that ILPostProcessor errors already block entering playmode.
// however, issues could still stop the weaving from running at all.
// WeaverFuse can check if it actually ran.
namespace Mirror
{
    public static class WeaverFuse
    {
        // this trick only works for ILPostProcessor.
        // CompilationFinishedHook can't weaver Mirror.dll.
        public static bool Weaved() =>
#if UNITY_2020_3_OR_NEWER
            false;
#else
            true;
#endif
    }
}
