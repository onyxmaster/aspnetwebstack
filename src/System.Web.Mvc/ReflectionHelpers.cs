// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Linq.Expressions;

namespace System.Reflection
{
    internal static class ReflectionHelpers
    {
        internal static Action<TTarget> CreateMethodCaller<TTarget>(string methodName)
        {
            var targetType = typeof(TTarget);
            var method = targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return null;
            }
            var target = Expression.Parameter(targetType, "target");
            var methodinvokeExpression = Expression.Call(target, method);
            var lambda = Expression.Lambda<Action<TTarget>>(methodinvokeExpression, new ParameterExpression[] { target });
            return lambda.Compile();
        }

        internal static Action<TTarget, TArg1> CreateMethodCaller<TTarget, TArg1>(string methodName)
        {
            var targetType = typeof(TTarget);
            var method = targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return null;
            }
            var target = Expression.Parameter(targetType, "target");
            var arg1 = Expression.Parameter(typeof(TArg1), "arg1");
            var invoke = Expression.Call(target, method, arg1);
            var lambda = Expression.Lambda<Action<TTarget, TArg1>>(invoke, new ParameterExpression[] { target, arg1 });
            return lambda.Compile();
        }

        internal static Func<TTarget, TResult> CreateMethodCallerWithResult<TTarget, TResult>(string methodName)
        {
            var targetType = typeof(TTarget);
            var method = targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return null;
            }
            var target = Expression.Parameter(targetType, "target");
            var invoke = Expression.Call(target, method);
            var lambda = Expression.Lambda<Func<TTarget, TResult>>(invoke, new ParameterExpression[] { target });
            return lambda.Compile();
        }

        internal static Func<TTarget, TArg1, TResult> CreateMethodCallerWithResult<TTarget, TArg1, TResult>(string methodName)
        {
            var targetType = typeof(TTarget);
            var method = targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return null;
            }
            var target = Expression.Parameter(targetType, "target");
            var arg1 = Expression.Parameter(typeof(TArg1), "arg1");
            var invoke = Expression.Call(target, method, arg1);
            var lambda = Expression.Lambda<Func<TTarget, TArg1, TResult>>(invoke, new ParameterExpression[] { target, arg1 });
            return lambda.Compile();
        }
    }
}
