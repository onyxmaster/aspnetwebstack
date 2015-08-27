// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace System.Web.Mvc.Html
{
    public static class PartialExtensions
    {
        public static MvcHtmlString Partial(this HtmlHelper htmlHelper, string partialViewName)
        {
            return Partial(htmlHelper, partialViewName, null /* model */, htmlHelper.ViewData);
        }

        public static MvcHtmlString Partial(this HtmlHelper htmlHelper, string partialViewName, ViewDataDictionary viewData)
        {
            return Partial(htmlHelper, partialViewName, null /* model */, viewData);
        }

        public static MvcHtmlString Partial(this HtmlHelper htmlHelper, string partialViewName, object model)
        {
            return Partial(htmlHelper, partialViewName, model, htmlHelper.ViewData);
        }

        public static MvcHtmlString Partial(this HtmlHelper htmlHelper, string partialViewName, object model, ViewDataDictionary viewData)
        {
            using (StringWriter writer = new StringWriter(CultureInfo.CurrentCulture))
            {
                htmlHelper.RenderPartialInternal(partialViewName, viewData, model, writer, ViewEngines.Engines);
                return MvcHtmlString.Create(writer.ToString());
            }
        }

        public static Task<MvcHtmlString> PartialAsync(this HtmlHelper htmlHelper, string partialViewName)
        {
            return PartialAsync(htmlHelper, partialViewName, null /* model */, htmlHelper.ViewData);
        }

        public static Task<MvcHtmlString> PartialAsync(this HtmlHelper htmlHelper, string partialViewName, ViewDataDictionary viewData)
        {
            return PartialAsync(htmlHelper, partialViewName, null /* model */, viewData);
        }

        public static Task<MvcHtmlString> PartialAsync(this HtmlHelper htmlHelper, string partialViewName, object model)
        {
            return PartialAsync(htmlHelper, partialViewName, model, htmlHelper.ViewData);
        }

        public static async Task<MvcHtmlString> PartialAsync(this HtmlHelper htmlHelper, string partialViewName, object model, ViewDataDictionary viewData)
        {
            using (StringWriter writer = new StringWriter(CultureInfo.CurrentCulture))
            {
                await htmlHelper.RenderPartialInternalAsync(partialViewName, viewData, model, writer, ViewEngines.Engines).ConfigureAwait(false);
                return MvcHtmlString.Create(writer.ToString());
            }
        }
    }
}
