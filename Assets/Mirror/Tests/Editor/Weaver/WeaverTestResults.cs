using System.Collections.Generic;

namespace Mirror.Weaver.Tests
{
    public class WeaverTestResults
    {
        public bool compileError;
        public bool weaverError;
        public List<string> weaverErrors = new List<string>();
        public List<string> weaverWarnings = new List<string>();

        public void CopyFrom(WeaverTestResults source)
        {
            compileError = source.compileError;
            weaverError = source.weaverError;

            weaverErrors.Clear();
            weaverErrors.AddRange(source.weaverErrors);

            weaverWarnings.Clear();
            weaverWarnings.AddRange(source.weaverWarnings);
        }

        public void Clear()
        {
            compileError = false;
            weaverError = false;
            weaverErrors.Clear();
            weaverWarnings.Clear();
        }

        public bool HasErrors()
        {
            return compileError
                || weaverError
                || weaverErrors.Count > 0;
        }
    }
}
