using System.IO;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Mirror.Weaver
{
    /// <summary>
    /// a compiled assembly,  used to work around ILPP problems with rewired
    /// </summary>
    internal class ILPostProcessCompiledAssembly : ICompiledAssembly
    {
        private readonly string m_AssemblyFilename;
        private readonly string m_OutputPath;
        private InMemoryAssembly m_InMemoryAssembly;

        public ILPostProcessCompiledAssembly(string asmName, string[] refs, string[] defines, string outputPath)
        {
            m_AssemblyFilename = asmName;
            Name = Path.GetFileNameWithoutExtension(m_AssemblyFilename);
            References = refs;
            Defines = defines;
            m_OutputPath = outputPath;
        }

        public string Name { get; }
        public string[] References { get; }
        public string[] Defines { get; }

        public InMemoryAssembly InMemoryAssembly
        {
            get
            {
                if (m_InMemoryAssembly == null)
                {
                    m_InMemoryAssembly = new InMemoryAssembly(
                        File.ReadAllBytes(Path.Combine(m_OutputPath, m_AssemblyFilename)),
                        File.ReadAllBytes(Path.Combine(m_OutputPath, $"{Path.GetFileNameWithoutExtension(m_AssemblyFilename)}.pdb")));
                }

                return m_InMemoryAssembly;
            }
        }
    }
}
