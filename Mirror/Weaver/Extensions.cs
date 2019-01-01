using Mono.Cecil;

namespace Mirror.Weaver
{
    public static class Extensions
    {
        public static bool IsDerivedFrom(this TypeDefinition td, TypeReference baseClass)
        {
            if (!td.IsClass)
                return false;

            // are ANY parent classes of baseClass?
            TypeReference parent = td.BaseType;
            while (parent != null)
            {
                string parentName = parent.FullName;

                // strip generic parameters
                int index = parentName.IndexOf('<');
                if (index != -1)
                {
                    parentName = parentName.Substring(0, index);
                }

                if (parentName == baseClass.FullName)
                {
                    return true;
                }
                try
                {
                    parent = parent.Resolve().BaseType;
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
                    //Console.WriteLine("AssemblyResolutionException: "+ ex.ToString());
                    break;
                }
            }
            return false;
        }
    }
}