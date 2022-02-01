// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Web.WebPages.Resources;

/*
WebPage class hierarchy

WebPageExecutingBase                        The base class for all Plan9 files (_pagestart, _appstart, and regular pages)
    ApplicationStartPage                    Used for _appstart.cshtml
    WebPageRenderingBase
        StartPage                           Used for _pagestart.cshtml
        WebPageBase
            WebPage                         Plan9Pages
            ViewWebPage?                    MVC Views
HelperPage                                  Base class for Web Pages in App_Code.
*/

namespace System.Web.WebPages
{
    // The base class for all CSHTML files (_pagestart, _appstart, and regular pages)
    public abstract class WebPageExecutingBase
    {
        private IVirtualPathFactory _virtualPathFactory;
        private DynamicHttpApplicationState _dynamicAppState;

        public virtual HttpApplicationStateBase AppState
        {
            get
            {
                if (Context != null)
                {
                    return Context.Application;
                }
                return null;
            }
        }

        public virtual dynamic App
        {
            get
            {
                if (_dynamicAppState == null && AppState != null)
                {
                    _dynamicAppState = new DynamicHttpApplicationState(AppState);
                }
                return _dynamicAppState;
            }
        }

        public virtual HttpContextBase Context { get; set; }

        public virtual string VirtualPath { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual IVirtualPathFactory VirtualPathFactory
        {
            get { return _virtualPathFactory ?? VirtualPathFactoryManager.Instance; }
            set { _virtualPathFactory = value; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public abstract void Execute();

        public virtual string Href(string path, params object[] pathParts)
        {
            return UrlUtil.GenerateClientUrl(Context, VirtualPath, path, pathParts);
        }

        internal virtual string GetDirectory(string virtualPath)
        {
            return VirtualPathUtility.GetDirectory(virtualPath);
        }

        /// <summary>
        /// Normalizes path relative to the current virtual path and throws if a file does not exist at the location.
        /// </summary>
        protected internal virtual string NormalizeLayoutPagePath(string layoutPagePath)
        {
            var virtualPath = NormalizePath(layoutPagePath);
            // Look for it as specified, either absolute, relative or same folder
            if (VirtualPathFactory.Exists(virtualPath))
            {
                return virtualPath;
            }
            throw new HttpException(String.Format(CultureInfo.CurrentCulture, WebPageResources.WebPage_LayoutPageNotFound, layoutPagePath, virtualPath));
        }

        public virtual string NormalizePath(string path)
        {
            // If it's relative, resolve it
            return VirtualPathUtility.Combine(VirtualPath, path);
        }

        public abstract void Write(HelperResult result);

        public abstract void Write(object value);

        public abstract void WriteLiteral(object value);

        public virtual void WriteAttribute(string name, string prefix, string suffix, params AttributeValue[] values)
        {
            WriteAttributeTo(GetOutputWriter(), name, prefix, suffix, values);
        }

        protected internal virtual void WriteAttributeTo(TextWriter writer, string name, string prefix, string suffix, params AttributeValue[] values)
        {
            bool first = true;
            bool wroteSomething = false;
            if (values.Length == 0)
            {
                // Explicitly empty attribute, so write the prefix and suffix
                WriteLiteralTo(writer, prefix);
                WriteLiteralTo(writer, suffix);
            }
            else
            {
                for (int i = 0; i < values.Length; i++)
                {
                    AttributeValue attrVal = values[i];
                    object val = attrVal.Value;
                    string next = i == values.Length - 1 ?
                        suffix : // End of the list, grab the suffix
                        values[i + 1].Prefix; // Still in the list, grab the next prefix

                    if (val == null)
                    {
                        // Nothing to write
                        continue;
                    }

                    // The special cases here are that the value we're writing might already be a string, or that the 
                    // value might be a bool. If the value is the bool 'true' we want to write the attribute name instead
                    // of the string 'true'. If the value is the bool 'false' we don't want to write anything.
                    //
                    // Otherwise the value is another object (perhaps an IHtmlString), and we'll ask it to format itself.
                    string stringValue;

                    // Intentionally using is+cast here for performance reasons. This is more performant than as+bool? 
                    // because of boxing.
                    if (val is bool)
                    {
                        if ((bool)val)
                        {
                            stringValue = name;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        stringValue = val as string;
                    }

                    if (first)
                    {
                        WriteLiteralTo(writer, prefix);
                        first = false;
                    }
                    else
                    {
                        WriteLiteralTo(writer, attrVal.Prefix);
                    }

                    // The extra branching here is to ensure that we call the Write*To(string) overload when
                    // possible.
                    if (attrVal.Literal && stringValue != null)
                    {
                        WriteLiteralTo(writer, stringValue);
                    }
                    else if (attrVal.Literal)
                    {
                        WriteLiteralTo(writer, val);
                    }
                    else if (stringValue != null)
                    {
                        WriteTo(writer, stringValue);
                    }
                    else
                    {
                        WriteTo(writer, val);
                    }

                    wroteSomething = true;
                }
                if (wroteSomething)
                {
                    WriteLiteralTo(writer, suffix);
                }
            }
        }

        // This method is called by generated code and needs to stay in sync with the parser
        public static void WriteTo(TextWriter writer, HelperResult content)
        {
            if (content != null)
            {
                content.WriteTo(writer);
            }
        }

        // This method is called by generated code and needs to stay in sync with the parser
        public static void WriteTo(TextWriter writer, object content)
        {
            writer.Write(HttpUtility.HtmlEncode(content));
        }

        // Perf optimization to avoid calling string.ToString when we already know the type is a string.
        private static void WriteTo(TextWriter writer, string content)
        {
            writer.Write(HttpUtility.HtmlEncode(content));
        }

        // This method is called by generated code and needs to stay in sync with the parser
        public static void WriteLiteralTo(TextWriter writer, object content)
        {
            writer.Write(content);
        }

        // Perf optimization to avoid calling string.ToString when we already know the type is a string.
        private static void WriteLiteralTo(TextWriter writer, string content)
        {
            writer.Write(content);
        }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "A method is more appropriate in this case since a property likely already exists to hold this value")]
        protected internal virtual TextWriter GetOutputWriter()
        {
            return TextWriter.Null;
        }
    }
}
