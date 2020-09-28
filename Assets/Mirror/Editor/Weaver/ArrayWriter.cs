using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public class ArrayWriter : CollectionWriter
    {
        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for type</exception>
        public ArrayWriter(TypeReference variable, int recursionCount) : base(variable, recursionCount) { }

        protected override string namePrefix => "Array";
        protected override bool needNullCheck => true;

        protected override TypeReference GetElementType(TypeReference variable)
        {
            return variable.GetElementType();
        }

        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for type</exception>
        protected override void ValidateType(TypeReference variable)
        {
            if (!variable.IsArrayType())
            {
                throw new GenerateWriterException($"{variable.Name} is an unsupported type. Jagged and multidimensional arrays are not supported", variable);
            }
        }

        protected override Instruction CreateLengthInstruction(ILProcessor worker)
        {
            return worker.Create(OpCodes.Ldlen);
        }
    }
}
