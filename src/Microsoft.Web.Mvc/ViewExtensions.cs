// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using ExpressionHelper = Microsoft.Web.Mvc.Internal.ExpressionHelper;

namespace Microsoft.Web.Mvc
{
    public static class ViewExtensions
    {
        [Obsolete("Child actions should be rendered asynchronously, use RenderRouteAsync instead.")]
        public static void RenderRoute(this HtmlHelper helper, RouteValueDictionary routeValues)
        {
            if (routeValues == null)
            {
                throw new ArgumentNullException("routeValues");
            }

            string actionName = (string)routeValues["action"];
            helper.RenderAction(actionName, routeValues);
        }

        public static Task RenderRouteAsync(this HtmlHelper helper, RouteValueDictionary routeValues)
        {
            if (routeValues == null)
            {
                throw new ArgumentNullException("routeValues");
            }

            string actionName = (string)routeValues["action"];
            return helper.RenderActionAsync(actionName, routeValues);
        }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is an appropriate nesting of generic types")]
        [Obsolete("Child actions should be rendered asynchronously, use RenderActionAsync instead.")]
        public static void RenderAction<TController>(this HtmlHelper helper, Expression<Action<TController>> action) where TController : Controller
        {
            var rvd = PrepareRvd(helper, action);

            RenderRoute(helper, rvd);
        }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is an appropriate nesting of generic types")]
        public static Task RenderActionAsync<TController>(this HtmlHelper helper, Expression<Action<TController>> action) where TController : Controller
        {
            var rvd = PrepareRvd(helper, action);

            return RenderRouteAsync(helper, rvd);
        }

        private static RouteValueDictionary PrepareRvd<TController>(HtmlHelper helper, Expression<Action<TController>> action) where TController : Controller
        {
            RouteValueDictionary rvd = ExpressionHelper.GetRouteValuesFromExpression(action);

            foreach (var entry in helper.ViewContext.RouteData.Values)
            {
                if (!rvd.ContainsKey(entry.Key))
                {
                    rvd.Add(entry.Key, entry.Value);
                }
            }
            return rvd;
        }
    }
}
