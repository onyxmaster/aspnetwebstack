// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Globalization;
using System.IO;
using System.Web.WebPages;

namespace System.Web.Mvc.Html
{
    public static class PartialExtensions
    {
        [Obsolete("Use RenderPartial.")]
        public static MvcHtmlString Partial(this HtmlHelper htmlHelper, string partialViewName)
        {
            return Partial(htmlHelper, partialViewName, null /* model */, htmlHelper.ViewData);
        }

        [Obsolete("Use RenderPartial.")]
        public static MvcHtmlString Partial(this HtmlHelper htmlHelper, string partialViewName, ViewDataDictionary viewData)
        {
            return Partial(htmlHelper, partialViewName, null /* model */, viewData);
        }

        [Obsolete("Use RenderPartial.")]
        public static MvcHtmlString Partial(this HtmlHelper htmlHelper, string partialViewName, object model)
        {
            return Partial(htmlHelper, partialViewName, model, htmlHelper.ViewData);
        }

        [Obsolete("Use RenderPartial.")]
        public static MvcHtmlString Partial(this HtmlHelper htmlHelper, string partialViewName, object model, ViewDataDictionary viewData)
        {
            using (var writer = new StringBlockWriter(CultureInfo.CurrentCulture))
            {
                htmlHelper.RenderPartialInternal(partialViewName, viewData, model, writer, ViewEngines.Engines);
                return MvcHtmlString.Create(writer.ToString());
            }
        }
    }
}
