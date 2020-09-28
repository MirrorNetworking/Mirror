using System;
using System.Collections.Generic;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    public static class Writers
    {
        const int MaxRecursionCount = 128;

        static Dictionary<string, MethodReference> writeFuncs;

        public static void Init()
        {
            writeFuncs = new Dictionary<string, MethodReference>();
        }

        public static void Register(TypeReference dataType, MethodReference methodReference)
        {
            writeFuncs[dataType.FullName] = methodReference;
        }

        static void RegisterWriteFunc(string name, MethodDefinition newWriterFunc)
        {
            writeFuncs[name] = newWriterFunc;
            Weaver.WeaveLists.generatedWriteFunctions.Add(newWriterFunc);

            Weaver.WeaveLists.ConfirmGeneratedCodeClass();
            Weaver.WeaveLists.generateContainerClass.Methods.Add(newWriterFunc);
        }

        /// <summary>
        /// Finds existing writer for type, if non exists trys to create one
        /// <para>This method is recursive</para>
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="recursionCount"></param>
        /// <returns>Returns <see cref="MethodReference"/> or null</returns>
        public static MethodReference GetWriteFunc(TypeReference variable, int recursionCount = 0)
        {
            if (writeFuncs.TryGetValue(variable.FullName, out MethodReference foundFunc))
            {
                return foundFunc;
            }
            else if (variable.Resolve().IsEnum)
            {
                // serialize enum as their base type
                return GetWriteFunc(variable.Resolve().GetEnumUnderlyingType());
            }
            else
            {
                MethodDefinition newWriterFunc = null;

                // this try/catch will be removed in future PR and make `GetWriteFunc` throw instead
                try
                {
                    newWriterFunc = GenerateWriter(variable, recursionCount);
                }
                catch (GenerateWriterException e)
                {
                    Weaver.Error(e.Message, e.MemberReference);
                }

                if (newWriterFunc != null)
                {
                    RegisterWriteFunc(variable.FullName, newWriterFunc);
                }
                return newWriterFunc;
            }
        }

        /// <exception cref="GenerateWriterException">Throws when writer could not be generated for type</exception>
        static MethodDefinition GenerateWriter(TypeReference variableReference, int recursionCount = 0)
        {
            // TODO: do we need this check? do we ever receieve types that are "ByReference"s
            if (variableReference.IsByReference)
            {
                throw new GenerateWriterException($"Cannot pass {variableReference.Name} by reference", variableReference);
            }

            // Arrays are special, if we resolve them, we get the element type,
            // eg int[] resolves to int
            // therefore process this before checks below
            if (variableReference.IsArray)
            {
                return new ArrayWriter(variableReference, recursionCount).Create();
            }

            // check for collections

            if (variableReference.Is(typeof(ArraySegment<>)))
            {
                return new ArraySegmentWriter(variableReference, recursionCount).Create();
            }
            if (variableReference.Is(typeof(List<>)))
            {
                return new ListWriter(variableReference, recursionCount).Create();
            }

            // check for invalid types

            TypeDefinition variableDefinition = variableReference.Resolve();
            if (variableDefinition == null)
            {
                throw new GenerateWriterException($"{variableReference.Name} is not a supported type. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableDefinition.IsDerivedFrom<UnityEngine.Component>())
            {
                throw new GenerateWriterException($"Cannot generate writer for component type {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableReference.Is<UnityEngine.Object>())
            {
                throw new GenerateWriterException($"Cannot generate writer for {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableReference.Is<UnityEngine.ScriptableObject>())
            {
                throw new GenerateWriterException($"Cannot generate writer for {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableDefinition.HasGenericParameters)
            {
                throw new GenerateWriterException($"Cannot generate writer for generic type {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableDefinition.IsInterface)
            {
                throw new GenerateWriterException($"Cannot generate writer for interface {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }
            if (variableDefinition.IsAbstract)
            {
                throw new GenerateWriterException($"Cannot generate writer for abstract class {variableReference.Name}. Use a supported type or provide a custom writer", variableReference);
            }

            // generate writer for class/struct

            return GenerateClassOrStructWriterFunction(variableReference, recursionCount);
        }

        static MethodDefinition GenerateClassOrStructWriterFunction(TypeReference variable, int recursionCount)
        {
            if (recursionCount > MaxRecursionCount)
            {
                throw new GenerateWriterException($"{variable.Name} can't be serialized because it references itself", variable);
            }

            string functionName = "_Write" + variable.Name + "_";
            if (variable.DeclaringType != null)
            {
                functionName += variable.DeclaringType.Name;
            }
            else
            {
                functionName += "None";
            }
            // create new writer for this type
            MethodDefinition writerFunc = new MethodDefinition(functionName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    WeaverTypes.Import(typeof(void)));

            writerFunc.Parameters.Add(new ParameterDefinition("writer", ParameterAttributes.None, WeaverTypes.Import<Mirror.NetworkWriter>()));
            writerFunc.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, Weaver.CurrentAssembly.MainModule.ImportReference(variable)));

            ILProcessor worker = writerFunc.Body.GetILProcessor();

            if (!WriteAllFields(variable, recursionCount, worker))
                return null;

            worker.Append(worker.Create(OpCodes.Ret));
            return writerFunc;
        }

        /// <summary>
        /// Find all fields in type and write them
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="recursionCount"></param>
        /// <param name="worker"></param>
        /// <returns>false if fail</returns>
        static bool WriteAllFields(TypeReference variable, int recursionCount, ILProcessor worker)
        {
            uint fields = 0;
            foreach (FieldDefinition field in variable.FindAllPublicFields())
            {
                MethodReference writeFunc = GetWriteFunc(field.FieldType, recursionCount + 1);
                // need this null check till later PR when GetWriteFunc throws exception instead
                if (writeFunc == null) { return false; }

                FieldReference fieldRef = Weaver.CurrentAssembly.MainModule.ImportReference(field);

                fields++;
                worker.Append(worker.Create(OpCodes.Ldarg_0));
                worker.Append(worker.Create(OpCodes.Ldarg_1));
                worker.Append(worker.Create(OpCodes.Ldfld, fieldRef));
                worker.Append(worker.Create(OpCodes.Call, writeFunc));
            }

            if (fields == 0)
            {
                Log.Warning($"{variable} has no no public or non-static fields to serialize");
            }

            return true;
        }
    }
}
