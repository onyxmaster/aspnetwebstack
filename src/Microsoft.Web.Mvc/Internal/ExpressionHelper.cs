﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Mvc.Routing;
using System.Web.Routing;
using Microsoft.Web.Mvc.Properties;

namespace Microsoft.Web.Mvc.Internal
{
    public static class ExpressionHelper
    {
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is an appropriate nesting of generic types")]
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "Users cannot use anonymous methods with the LambdaExpression type")]
        public static RouteValueDictionary GetRouteValuesFromExpression<TController>(Expression<Action<TController>> action) where TController : Controller
        {
            var call = GetMethodCall(action);

            var controllerType = typeof(TController);
            var controllerName = ValidateControllerName(controllerType);

            var rvd = new RouteValueDictionary();
            AddControllerInfoToDictionary(rvd, call, controllerName, controllerType);
            AddParameterValuesFromExpressionToDictionary(rvd, call);
            return rvd;
        }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is an appropriate nesting of generic types")]
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "Users cannot use anonymous methods with the LambdaExpression type")]
        public static KeyValuePair<string, RouteValueDictionary> GetRouteInfoFromExpression<TController>(RouteCollection routeCollection, Expression<Action<TController>> action) where TController : Controller
        {
            var call = GetMethodCall(action);

            var controllerType = typeof(TController);
            var controllerName = ValidateControllerName(controllerType);

            string routeName;
            if (call.Method.DeclaringType == controllerType)
            {
                routeName = _routeNameCache.GetName(call.Method);
                if (routeName != null && routeCollection[routeName] == null)
                {
                    routeName = null;
                }
            }
            else
            {
                routeName = null;
            }

            var rvd = new RouteValueDictionary();
            if (routeName == null)
            {
                AddControllerInfoToDictionary(rvd, call, controllerName, controllerType);
            }
            AddParameterValuesFromExpressionToDictionary(rvd, call);
            return new KeyValuePair<string, RouteValueDictionary>(routeName, rvd);
        }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is an appropriate nesting of generic types")]
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "Users cannot use anonymous methods with the LambdaExpression type")]
        public static string GetInputName<TModel, TProperty>(Expression<Func<TModel, TProperty>> expression)
        {
            if (expression.Body.NodeType == ExpressionType.Call)
            {
                MethodCallExpression methodCallExpression = (MethodCallExpression)expression.Body;
                string name = GetInputName(methodCallExpression);
                return name.Substring(expression.Parameters[0].Name.Length + 1);
            }
            return expression.Body.ToString().Substring(expression.Parameters[0].Name.Length + 1);
        }

        private static string GetInputName(MethodCallExpression expression)
        {
            // p => p.Foo.Bar().Baz.ToString() => p.Foo OR throw...

            MethodCallExpression methodCallExpression = expression.Object as MethodCallExpression;
            if (methodCallExpression != null)
            {
                return GetInputName(methodCallExpression);
            }
            return expression.Object.ToString();
        }

        // This method contains some heuristics that will help determine the correct action name from a given MethodInfo
        // assuming the default sync / async invokers are in use. The logic's not foolproof, but it should be good enough
        // for most uses.
        private static string GetTargetActionName(MethodInfo methodInfo)
        {
            string methodName = methodInfo.Name;

            // do we know this not to be an action?
            if (methodInfo.IsDefined(typeof(NonActionAttribute), true /* inherit */))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                  MvcResources.ExpressionHelper_CannotCallNonAction, methodName));
            }

            // has this been renamed?
            ActionNameAttribute nameAttr = methodInfo.GetCustomAttributes(typeof(ActionNameAttribute), true /* inherit */).OfType<ActionNameAttribute>().FirstOrDefault();
            if (nameAttr != null)
            {
                return nameAttr.Name;
            }

            // targeting an async action?
            if (methodInfo.DeclaringType.IsSubclassOf(typeof(AsyncController)))
            {
                if (methodName.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
                {
                    return methodName.Substring(0, methodName.Length - "Async".Length);
                }
                if (methodName.EndsWith("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                                                                      MvcResources.ExpressionHelper_CannotCallCompletedMethod, methodName));
                }
            }

            // fallback
            return methodName;
        }

        private static void AddParameterValuesFromExpressionToDictionary(RouteValueDictionary rvd, MethodCallExpression call)
        {
            ParameterInfo[] parameters = call.Method.GetParameters();

            if (parameters.Length > 0)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    Expression arg = call.Arguments[i];
                    object value = null;
                    ConstantExpression ce = arg as ConstantExpression;
                    if (ce != null)
                    {
                        // If argument is a constant expression, just get the value
                        value = ce.Value;
                    }
                    else
                    {
                        value = CachedExpressionCompiler.Evaluate(arg);
                    }
                    rvd.Add(parameters[i].Name, value);
                }
            }
        }

        private sealed class RouteNameCache : ReaderWriterCache<MethodInfo, string>
        {
            public string GetName(MethodInfo method)
            {
                return FetchOrCreateItem(method, () => NameCreator(method));
            }

            private static string NameCreator(MethodInfo method)
            {
                string template = null;
                foreach (IRouteInfoProvider attr in method.GetCustomAttributes(typeof(IRouteInfoProvider), false))
                {
                    if (template == null)
                    {
                        template = attr.Template;
                    }
                    var name = attr.Name;
                    if (!String.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
                if (template == null)
                {
                    foreach (IRouteInfoProvider attr in method.DeclaringType.GetCustomAttributes(typeof(IRouteInfoProvider), false))
                    {
                        if (template == null)
                        {
                            template = attr.Template;
                        }
                        var name = attr.Name;
                        if (!String.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }
                return "MethodInfo!" + method.MethodHandle.Value + "_" + template;
            }
        }

        private static readonly RouteNameCache _routeNameCache = new RouteNameCache();

        [SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly", Justification = "Validation method.")]
        private static string ValidateControllerName(Type controllerType)
        {
            var controllerName = controllerType.Name;
            if (!controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(MvcResources.ExpressionHelper_TargetMustEndInController, "action");
            }
            controllerName = controllerName.Substring(0, controllerName.Length - "Controller".Length);
            if (controllerName.Length == 0)
            {
                throw new ArgumentException(MvcResources.ExpressionHelper_CannotRouteToController, "action");
            }

            return controllerName;
        }

        private static void AddControllerInfoToDictionary(RouteValueDictionary rvd, MethodCallExpression call, string controllerName, Type controllerType)
        {
            // TODO: How do we know that this method is even web callable?
            //      For now, we just let the call itself throw an exception.

            string actionName = GetTargetActionName(call.Method);

            rvd.Add("Controller", controllerName);
            rvd.Add("Action", actionName);

            ActionLinkAreaAttribute areaAttr = controllerType.GetCustomAttributes(typeof(ActionLinkAreaAttribute), true /* inherit */).FirstOrDefault() as ActionLinkAreaAttribute;
            if (areaAttr != null)
            {
                string areaName = areaAttr.Area;
                rvd.Add("Area", areaName);
            }
        }

        private static MethodCallExpression GetMethodCall<TController>(Expression<Action<TController>> action) where TController : Controller
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            MethodCallExpression call = action.Body as MethodCallExpression;
            if (call == null)
            {
                throw new ArgumentException(MvcResources.ExpressionHelper_MustBeMethodCall, "action");
            }

            return call;
        }
    }
}
