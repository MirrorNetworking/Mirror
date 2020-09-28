using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public class ArraySegmentWriter : CollectionWriter
    {
        GenericInstanceType genericInstance;

        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for type</exception>
        public ArraySegmentWriter(TypeReference variable, int recursionCount) : base(variable, recursionCount) { }

        protected override string Name => "ArraySegment";
        protected override bool NeedNullCheck => false;

        protected override TypeReference GetElementType(TypeReference variable)
        {
            genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];
            return elementType;
        }

        protected override Instruction CreateLengthInstruction(ILProcessor worker)
        {
            MethodReference countref = WeaverTypes.ArraySegmentCountReference.MakeHostInstanceGeneric(genericInstance);

            return worker.Create(OpCodes.Call, countref);
        }

        protected override void AppendCollection(ILProcessor worker)
        {
            MethodReference getArray = WeaverTypes.ArraySegmentArrayReference.MakeHostInstanceGeneric(genericInstance);

            // arg1 is the segment
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            // value.Array
            worker.Append(worker.Create(OpCodes.Call, getArray));
        }

        protected override void AppendIndex(ILProcessor worker)
        {
            MethodReference getOffset = WeaverTypes.ArraySegmentOffsetReference.MakeHostInstanceGeneric(genericInstance);

            // local 1 is i
            worker.Append(worker.Create(OpCodes.Ldloc_1));

            // value.Offset
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Call, getOffset));

            // add (i + offset)
            worker.Append(worker.Create(OpCodes.Add));
        }
    }
}
