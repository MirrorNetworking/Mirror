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
            if (expression.Body is MethodCallExpression outermostExpression)
            {
                MethodInfo methodInfo = outermostExpression.Method;
                return module.ImportReference(methodInfo);
            }

            if (expression.Body is NewExpression newExpression)
            {
                ConstructorInfo methodInfo = newExpression.Constructor;
                // constructor is null when creating an ArraySegment<object>
                methodInfo = methodInfo ?? newExpression.Type.GetConstructors()[0];
                return module.ImportReference(methodInfo);
            }

            if (expression.Body is MemberExpression memberExpression)
            {
                var property = memberExpression.Member as PropertyInfo;
                return module.ImportReference(property.GetGetMethod());
            }

            throw new ArgumentException($"Invalid Expression {expression.Body.GetType()}");
        }
    }
}