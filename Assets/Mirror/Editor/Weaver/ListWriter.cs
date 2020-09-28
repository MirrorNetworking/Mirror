using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public class ListWriter : CollectionWriter
    {
        GenericInstanceType genericInstance;

        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for type</exception>
        public ListWriter(TypeReference variable, int recursionCount) : base(variable, recursionCount) { }

        protected override string namePrefix => "List";
        protected override bool needNullCheck => true;

        protected override TypeReference GetElementType(TypeReference variable)
        {
            genericInstance = (GenericInstanceType)variable;
            TypeReference elementType = genericInstance.GenericArguments[0];
            return elementType;
        }

        protected override Instruction CreateLengthInstruction(ILProcessor worker)
        {
            MethodReference countref = WeaverTypes.ListCountReference.MakeHostInstanceGeneric(genericInstance);

            return worker.Create(OpCodes.Call, countref);
        }

        protected override void AppendGetElement(ILProcessor worker)
        {
            MethodReference getItem = WeaverTypes.ListGetItemReference.MakeHostInstanceGeneric(genericInstance);

            // call get_Item
            // list and index should be already on stack
            worker.Append(worker.Create(OpCodes.Call, getItem));
        }

    }
}
