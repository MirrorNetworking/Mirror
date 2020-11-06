using System;
using System.Linq.Expressions;
using System.Reflection;
using Mono.Cecil;

namespace Mirror.Weaver
{
    public static class ModuleExtension
    {
        public static MethodReference ImportReference(this ModuleDefinition module, Expression<Action> expression) => ImportReference(module, (LambdaExpression)expression);
        public static MethodReference ImportReference<T>(this ModuleDefinition module, Expression<Action<T>> expression) => ImportReference(module, (LambdaExpression)expression);

        public static MethodReference ImportReference(this ModuleDefinition module, LambdaExpression expression)
        {
            if (!(expression.Body is MethodCallExpression outermostExpression))
            {
                throw new ArgumentException("Invalid Expression. Expression should consist of a Method call only.");
            }

            MethodInfo methodInfo = outermostExpression.Method;

            return module.ImportReference(methodInfo);
        }
    }
}