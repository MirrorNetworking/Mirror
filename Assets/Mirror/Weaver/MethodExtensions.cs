using System;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirror.Weaver
{
    /// <summary>
    /// Convenient extensions for modifying methods
    /// </summary>
    public static class MethodExtensions
    {
        public static ParameterDefinition AddParam<T>(this MethodDefinition method, string name, ParameterAttributes attributes = ParameterAttributes.None)
            => AddParam(method, method.Module.ImportReference(typeof(T)), name, attributes);

        public static ParameterDefinition AddParam(this MethodDefinition method, TypeReference typeRef, string name, ParameterAttributes attributes = ParameterAttributes.None)
        {
            var param = new ParameterDefinition(name, attributes, typeRef);
            method.Parameters.Add(param);
            return param;
        }

        public static VariableDefinition AddLocal<T>(this MethodDefinition method) => AddLocal(method, method.Module.ImportReference(typeof(T)));

        public static VariableDefinition AddLocal(this MethodDefinition method, TypeReference type)
        {
            var local = new VariableDefinition(type);
            method.Body.Variables.Add(local);
            return local;
        }

        public static Instruction Create(this ILProcessor worker, OpCode code, Expression<Action> expression)
        {
            MethodReference typeref = worker.Body.Method.Module.ImportReference(expression);
            return worker.Create(code, typeref);
        }

        public static Instruction Create<T>(this ILProcessor worker, OpCode code, Expression<Action<T>> expression)
        {
            MethodReference typeref = worker.Body.Method.Module.ImportReference(expression);
            return worker.Create(code, typeref);
        }

        public static Instruction Create<T, TR>(this ILProcessor worker, OpCode code, Expression<Func<T, TR>> expression)
        {
            MethodReference typeref = worker.Body.Method.Module.ImportReference(expression);
            return worker.Create(code, typeref);
        }

        public static SequencePoint GetSequencePoint(this MethodDefinition method, Instruction instruction)
        {
            SequencePoint sequencePoint = method.DebugInformation.GetSequencePoint(instruction);
            return sequencePoint;
        }
    }
}