// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace System.Web.Mvc.Html
{
    public static class RenderPartialExtensions
    {
        // Renders the partial view with the parent's view data and model
        [Obsolete("Partials should be rendered asynchronously, use RenderPartialAsync instead.")]
        public static void RenderPartial(this HtmlHelper htmlHelper, string partialViewName)
        {
            htmlHelper.RenderPartialInternal(partialViewName, htmlHelper.ViewData, null /* model */, htmlHelper.ViewContext.Writer, ViewEngines.Engines);
        }

        // Renders the partial view with the given view data and, implicitly, the given view data's model
        [Obsolete("Partials should be rendered asynchronously, use RenderPartialAsync instead.")]
        public static void RenderPartial(this HtmlHelper htmlHelper, string partialViewName, ViewDataDictionary viewData)
        {
            htmlHelper.RenderPartialInternal(partialViewName, viewData, null /* model */, htmlHelper.ViewContext.Writer, ViewEngines.Engines);
        }

        // Renders the partial view with an empty view data and the given model
        [Obsolete("Partials should be rendered asynchronously, use RenderPartialAsync instead.")]
        public static void RenderPartial(this HtmlHelper htmlHelper, string partialViewName, object model)
        {
            htmlHelper.RenderPartialInternal(partialViewName, htmlHelper.ViewData, model, htmlHelper.ViewContext.Writer, ViewEngines.Engines);
        }

        // Renders the partial view with a copy of the given view data plus the given model
        [Obsolete("Partials should be rendered asynchronously, use RenderPartialAsync instead.")]
        public static void RenderPartial(this HtmlHelper htmlHelper, string partialViewName, object model, ViewDataDictionary viewData)
        {
            htmlHelper.RenderPartialInternal(partialViewName, viewData, model, htmlHelper.ViewContext.Writer, ViewEngines.Engines);
        }

        public static Task RenderPartialAsync(this HtmlHelper htmlHelper, string partialViewName)
        {
            return htmlHelper.RenderPartialInternalAsync(partialViewName, htmlHelper.ViewData, null /* model */, htmlHelper.ViewContext.Writer, ViewEngines.Engines);
        }

        // Renders the partial view with the given view data and, implicitly, the given view data's model
        public static Task RenderPartialAsync(this HtmlHelper htmlHelper, string partialViewName, ViewDataDictionary viewData)
        {
            return htmlHelper.RenderPartialInternalAsync(partialViewName, viewData, null /* model */, htmlHelper.ViewContext.Writer, ViewEngines.Engines);
        }

        // Renders the partial view with an empty view data and the given model
        public static Task RenderPartialAsync(this HtmlHelper htmlHelper, string partialViewName, object model)
        {
            return htmlHelper.RenderPartialInternalAsync(partialViewName, htmlHelper.ViewData, model, htmlHelper.ViewContext.Writer, ViewEngines.Engines);
        }

        // Renders the partial view with a copy of the given view data plus the given model
        public static Task RenderPartialAsync(this HtmlHelper htmlHelper, string partialViewName, object model, ViewDataDictionary viewData)
        {
            return htmlHelper.RenderPartialInternalAsync(partialViewName, viewData, model, htmlHelper.ViewContext.Writer, ViewEngines.Engines);
        }
    }
}
