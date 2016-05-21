// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.WebPages.Resources;
using Microsoft.Internal.Web.Utils;

namespace System.Web.WebPages
{
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "This is temporary (elipton)")]
    public abstract class WebPageBase : WebPageRenderingBase
    {
        // Keep track of which sections RenderSection has already been called on
        private readonly HashSet<string> _renderedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Keep track of whether RenderBody has been called
        private bool _renderedBody = false;
        // Action for rendering the body within a layout page
        private Action<TextWriter> _body;

        private StringBlockWriter _tempWriter;
        private TextWriter _currentWriter;

        private DynamicPageDataDictionary<dynamic> _dynamicPageData;

        public override string Layout { get; set; }

        public TextWriter Output
        {
            get { return OutputStack.Peek(); }
        }

        public Stack<TextWriter> OutputStack
        {
            get { return PageContext.OutputStack; }
        }

        public override IDictionary<object, dynamic> PageData
        {
            get { return PageContext.PageData; }
        }

        public override dynamic Page
        {
            get
            {
                if (_dynamicPageData == null)
                {
                    _dynamicPageData = new DynamicPageDataDictionary<dynamic>((PageDataDictionary<dynamic>)PageData);
                }
                return _dynamicPageData;
            }
        }

        // Retrieves the sections defined in the calling page. If this is null, that means
        // this page has been requested directly.
        private Dictionary<string, SectionWriter> PreviousSectionWriters
        {
            get
            {
                var top = SectionWritersStack.Pop();
                var previous = SectionWritersStack.Count > 0 ? SectionWritersStack.Peek() : null;
                SectionWritersStack.Push(top);
                return previous;
            }
        }

        // Retrieves the current Dictionary of sectionWriters on the stack without poping it.
        // There should be at least one on the stack which is added when the Render(ViewData,TextWriter)
        // is called.
        private Dictionary<string, SectionWriter> SectionWriters
        {
            get { return SectionWritersStack.Peek(); }
        }

        private Stack<Dictionary<string, SectionWriter>> SectionWritersStack
        {
            get { return PageContext.SectionWritersStack; }
        }

        protected virtual void ConfigurePage(WebPageBase parentPage)
        {
        }

        public static WebPageBase CreateInstanceFromVirtualPath(string virtualPath)
        {
            return CreateInstanceFromVirtualPath(virtualPath, VirtualPathFactoryManager.Instance);
        }

        internal static WebPageBase CreateInstanceFromVirtualPath(string virtualPath, IVirtualPathFactory virtualPathFactory)
        {
            // Get the compiled object
            try
            {
                WebPageBase webPage = virtualPathFactory.CreateInstance<WebPageBase>(virtualPath);

                // Give it its virtual path
                webPage.VirtualPath = virtualPath;

                // Assign it the VirtualPathFactory
                webPage.VirtualPathFactory = virtualPathFactory;

                return webPage;
            }
            catch (HttpException e)
            {
                BuildManagerExceptionUtil.ThrowIfUnsupportedExtension(virtualPath, e);
                throw;
            }
        }

        /// <summary>
        /// Attempts to create a WebPageBase instance from a virtualPath and wraps complex compiler exceptions with simpler messages
        /// </summary>
        protected virtual WebPageBase CreatePageFromVirtualPath(string virtualPath, HttpContextBase httpContext, Func<string, bool> virtualPathExists, DisplayModeProvider displayModeProvider, IDisplayMode displayMode)
        {
            try
            {
                DisplayInfo resolvedDisplayInfo = displayModeProvider.GetDisplayInfoForVirtualPath(virtualPath, httpContext, virtualPathExists, displayMode);

                if (resolvedDisplayInfo != null)
                {
                    var webPage = VirtualPathFactory.CreateInstance<WebPageBase>(resolvedDisplayInfo.FilePath);

                    if (webPage != null)
                    {
                        // Give it its virtual path
                        webPage.VirtualPath = virtualPath;
                        webPage.VirtualPathFactory = VirtualPathFactory;
                        webPage.DisplayModeProvider = DisplayModeProvider;

                        return webPage;
                    }
                }
            }
            catch (HttpException e)
            {
                // If the path uses an unregistered extension, such as Foo.txt,
                // then an error regarding build providers will be thrown.
                // Check if this is the case and throw a simpler error.
                BuildManagerExceptionUtil.ThrowIfUnsupportedExtension(virtualPath, e);

                // If the path uses an extension registered with codedom, such as Foo.js,
                // then an unfriendly compilation error might get thrown by the underlying compiler.
                // Check if this is the case and throw a simpler error.
                BuildManagerExceptionUtil.ThrowIfCodeDomDefinedExtension(virtualPath, e);

                // Rethrow any errors
                throw;
            }
            // The page is missing, could not be compiled or is of an invalid type.
            throw new HttpException(String.Format(CultureInfo.CurrentCulture, WebPageResources.WebPage_InvalidPageType, virtualPath));
        }

        private WebPageContext CreatePageContextFromParameters(bool isLayoutPage, params object[] data)
        {
            object first = null;
            if (data != null && data.Length > 0)
            {
                first = data[0];
            }

            var pageData = PageDataDictionary<dynamic>.CreatePageDataFromParameters(PageData, data);

            return WebPageContext.CreateNestedPageContext(PageContext, pageData, first, isLayoutPage);
        }

        public void DefineSection(string name, SectionWriter action)
        {
            if (SectionWriters.ContainsKey(name))
            {
                throw new HttpException(String.Format(CultureInfo.InvariantCulture, WebPageResources.WebPage_SectionAleadyDefined, name));
            }
            SectionWriters[name] = action;
        }

        internal void EnsurePageCanBeRequestedDirectly(string methodName)
        {
            if (PreviousSectionWriters == null)
            {
                throw new HttpException(String.Format(CultureInfo.CurrentCulture, WebPageResources.WebPage_CannotRequestDirectly, VirtualPath, methodName));
            }
        }

        public void ExecutePageHierarchy(WebPageContext pageContext, TextWriter writer)
        {
            ExecutePageHierarchy(pageContext, writer, startPage: null);
        }

        public Task ExecutePageHierarchyAsync(WebPageContext pageContext, TextWriter writer)
        {
            return ExecutePageHierarchyAsync(pageContext, writer, startPage: null);
        }

        // This method is only used by WebPageBase to allow passing in the view context and writer.
        public void ExecutePageHierarchy(WebPageContext pageContext, TextWriter writer, WebPageRenderingBase startPage)
        {
            PushContext(pageContext, writer);

            if (PrepareStartPage(pageContext, startPage))
            {
                startPage.ExecutePageHierarchy();
            }
            else
            {
                ExecutePageHierarchy();
            }
            PopContext();
        }

        public async Task ExecutePageHierarchyAsync(WebPageContext pageContext, TextWriter writer, WebPageRenderingBase startPage)
        {
            PushContext(pageContext, writer);

            if (PrepareStartPage(pageContext, startPage))
            {
                await startPage.ExecutePageHierarchyAsync().ConfigureAwait(false);
            }
            else
            {
                await ExecutePageHierarchyAsync().ConfigureAwait(false);
            }
            await PopContextAsync().ConfigureAwait(false);
        }

        private bool PrepareStartPage(WebPageContext pageContext, WebPageRenderingBase startPage)
        {
            if (startPage != null && startPage != this)
            {
                var startPageContext = WebPageContext.CreateNestedPageContext<object>(parentContext: pageContext, pageData: null, model: null, isLayoutPage: false);
                startPageContext.Page = startPage;
                startPage.PageContext = startPageContext;
                return true;
            }
            return false;
        }

        public override void ExecutePageHierarchy()
        {
            PreparePageHierarchy();

            TemplateStack.Push(Context, this);
            try
            {
                // Execute the developer-written code of the WebPage
                ExecutePage();
            }
            finally
            {
                TemplateStack.Pop(Context);
            }
        }

        public async override Task ExecutePageHierarchyAsync()
        {
            PreparePageHierarchy();

            TemplateStack.Push(Context, this);
            try
            {
                // Execute the developer-written code of the WebPage
                var task = ExecuteAsync();
                if (task != null)
                {
                    await task.ConfigureAwait(false);
                }
                else
                {
                    Execute();
                }
            }
            finally
            {
                TemplateStack.Pop(Context);
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We really don't care if SourceHeader fails, and we don't want it to fail any real requests ever")]
        private void PreparePageHierarchy()
        {
            // Unlike InitPages, for a WebPage there is no hierarchy - it is always
            // the last file to execute in the chain. There can still be layout pages
            // and partial pages, but they are never part of the hierarchy.

            // (add server header for falcon debugging)
            // call to MapPath() is expensive. If we are not emiting source files to header, 
            // don't bother to populate the SourceFiles collection. This saves perf significantly.
            if (WebPageHttpHandler.ShouldGenerateSourceHeader(Context))
            {
                try
                {
                    string vp = VirtualPath;
                    if (vp != null)
                    {
                        string path = Context.Request.MapPath(vp);
                        if (!path.IsEmpty())
                        {
                            PageContext.SourceFiles.Add(path);
                        }
                    }
                }
                catch
                {
                    // we really don't care if this ever fails, so we swallow all exceptions
                }
            }
        }

        protected virtual void InitializePage()
        {
        }

        public bool IsSectionDefined(string name)
        {
            EnsurePageCanBeRequestedDirectly("IsSectionDefined");
            return PreviousSectionWriters.ContainsKey(name);
        }

        public void PopContext()
        {
            // Using the CopyTo extension method on the _tempWriter instead of .ToString()
            // to avoid allocating large strings that then end up on the Large object heap.
            OutputStack.Pop();

            if (!String.IsNullOrEmpty(Layout))
            {
                string layoutPagePath = NormalizeLayoutPagePath(Layout);

                // If a layout file was specified, render it passing our page content.
                OutputStack.Push(_currentWriter);
                RenderSurrounding(
                    layoutPagePath,
                    _tempWriter.CopyTo);
                OutputStack.Pop();
            }
            else
            {
                // Otherwise, just render the page.
                _tempWriter.CopyTo(_currentWriter);
            }

            VerifyRenderedBodyOrSections();
            SectionWritersStack.Pop();
        }

        public async Task PopContextAsync()
        {
            // Using the CopyTo extension method on the _tempWriter instead of .ToString()
            // to avoid allocating large strings that then end up on the Large object heap.
            OutputStack.Pop();

            if (!String.IsNullOrEmpty(Layout))
            {
                string layoutPagePath = NormalizeLayoutPagePath(Layout);

                // If a layout file was specified, render it passing our page content.
                OutputStack.Push(_currentWriter);
                await RenderSurroundingAsync(
                    layoutPagePath,
                    _tempWriter.CopyTo).ConfigureAwait(false);
                OutputStack.Pop();
            }
            else
            {
                // Otherwise, just render the page.
                _tempWriter.CopyTo(_currentWriter);
            }

            VerifyRenderedBodyOrSections();
            SectionWritersStack.Pop();
        }

        public void PushContext(WebPageContext pageContext, TextWriter writer)
        {
            _currentWriter = writer;
            PageContext = pageContext;
            pageContext.Page = this;

            InitializePage();

            // Create a temporary writer
            _tempWriter = new StringBlockWriter(CultureInfo.InvariantCulture);

            // Render the page into it
            OutputStack.Push(_tempWriter);
            SectionWritersStack.Push(new Dictionary<string, SectionWriter>(StringComparer.OrdinalIgnoreCase));

            // If the body is defined in the ViewData, remove it and store it on the instance
            // so that it won't affect rendering of partial pages when they call VerifyRenderedBodyOrSections
            if (PageContext.BodyAction != null)
            {
                _body = PageContext.BodyAction;
                PageContext.BodyAction = null;
            }
        }

        public HelperResult RenderBody()
        {
            EnsurePageCanBeRequestedDirectly("RenderBody");

            if (_renderedBody)
            {
                throw new HttpException(WebPageResources.WebPage_RenderBodyAlreadyCalled);
            }
            _renderedBody = true;

            // _body should have previously been set in Render(ViewContext,TextWriter) if it
            // was available in the ViewData.
            if (_body != null)
            {
                return new HelperResult(tw => _body(tw));
            }
            else
            {
                throw new HttpException(String.Format(CultureInfo.CurrentCulture, WebPageResources.WebPage_CannotRequestDirectly, VirtualPath, "RenderBody"));
            }
        }

        public override HelperResult RenderPage(string path, params object[] data)
        {
            return RenderPageCore(path, isLayoutPage: false, data: data);
        }

        public override Task<HelperResult> RenderPageAsync(string path, params object[] data)
        {
            return RenderPageCoreAsync(path, isLayoutPage: false, data: data);
        }

        private WebPageBase PrepareRenderPage(string path, bool isLayoutPage, object[] data, out WebPageContext pageContext)
        {
            path = NormalizePath(path);
            var subPage = CreatePageFromVirtualPath(path, Context, VirtualPathFactory.Exists, DisplayModeProvider, DisplayMode);
            pageContext = CreatePageContextFromParameters(isLayoutPage, data);
            subPage.ConfigurePage(this);
            return subPage;
        }

        private HelperResult RenderPageCore(string path, bool isLayoutPage, object[] data)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "path");
            }

            return new HelperResult(writer =>
            {
                WebPageContext pageContext;
                var subPage = PrepareRenderPage(path, isLayoutPage, data, out pageContext);
                subPage.ExecutePageHierarchy(pageContext, writer);
            });
        }

        private async Task<HelperResult> RenderPageCoreAsync(string path, bool isLayoutPage, object[] data)
        {
            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "path");
            }
            WebPageContext pageContext;
            var subPage = PrepareRenderPage(path, isLayoutPage, data, out pageContext);
            var writer = new StringBlockWriter(_currentWriter.FormatProvider);
            await subPage.ExecutePageHierarchyAsync(pageContext, writer).ConfigureAwait(false);
            return new HelperResult(writer.CopyTo);
        }

        public HelperResult RenderSection(string name)
        {
            return RenderSection(name, required: true);
        }

        public HelperResult RenderSection(string name, bool required)
        {
            EnsurePageCanBeRequestedDirectly("RenderSection");

            if (PreviousSectionWriters.ContainsKey(name))
            {
                var result = new HelperResult(tw =>
                {
                    if (_renderedSections.Contains(name))
                    {
                        throw new HttpException(String.Format(CultureInfo.InvariantCulture, WebPageResources.WebPage_SectionAleadyRendered, name));
                    }
                    var body = PreviousSectionWriters[name];
                    // Since the body can also call RenderSection, we need to temporarily remove
                    // the current sections from the stack.
                    var top = SectionWritersStack.Pop();

                    bool pushed = false;
                    try
                    {
                        if (Output != tw)
                        {
                            OutputStack.Push(tw);
                            pushed = true;
                        }

                        body();
                    }
                    finally
                    {
                        if (pushed)
                        {
                            OutputStack.Pop();
                        }
                    }
                    SectionWritersStack.Push(top);
                    _renderedSections.Add(name);
                });
                return result;
            }
            else if (required)
            {
                // If the section is not found, and it is not optional, throw an error.
                throw new HttpException(String.Format(CultureInfo.InvariantCulture, WebPageResources.WebPage_SectionNotDefined, name));
            }
            else
            {
                // If the section is optional and not found, then don't do anything.
                return null;
            }
        }

        private void RenderSurrounding(string partialViewName, Action<TextWriter> body)
        {
            // Save the previous body action and set ours instead.
            // This value will be retrieved by the sub-page being rendered when it runs
            // Render(ViewData, TextWriter).
            var priorValue = PageContext.BodyAction;
            PageContext.BodyAction = body;

            // Render the layout file
            Write(RenderPageCore(partialViewName, isLayoutPage: true, data: new object[0]));

            // Restore the state
            PageContext.BodyAction = priorValue;
        }

        private async Task RenderSurroundingAsync(string partialViewName, Action<TextWriter> body)
        {
            // Save the previous body action and set ours instead.
            // This value will be retrieved by the sub-page being rendered when it runs
            // Render(ViewData, TextWriter).
            var priorValue = PageContext.BodyAction;
            PageContext.BodyAction = body;

            // Render the layout file
            Write(await RenderPageCoreAsync(partialViewName, isLayoutPage: true, data: new object[0]).ConfigureAwait(false));

            // Restore the state
            PageContext.BodyAction = priorValue;
        }

        // Verifies that RenderBody is called, or that RenderSection is called for all sections
        private void VerifyRenderedBodyOrSections()
        {
            // The _body will be set within a layout page because PageContext.BodyAction was set by RenderSurrounding, 
            // which is only called in the case of rendering layout pages.
            // Using RenderPage will not result in a _body being set in a partial page, thus the following checks for
            // sections should not apply when RenderPage is called.
            // Dev10 bug 928341 
            if (_body != null)
            {
                if (SectionWritersStack.Count > 1 && PreviousSectionWriters != null && PreviousSectionWriters.Count > 0)
                {
                    // There are sections defined. Check that all sections have been rendered.
                    StringBuilder sectionsNotRendered = new StringBuilder();
                    foreach (var name in PreviousSectionWriters.Keys)
                    {
                        if (!_renderedSections.Contains(name))
                        {
                            if (sectionsNotRendered.Length > 0)
                            {
                                sectionsNotRendered.Append("; ");
                            }
                            sectionsNotRendered.Append(name);
                        }
                    }
                    if (sectionsNotRendered.Length > 0)
                    {
                        throw new HttpException(String.Format(CultureInfo.CurrentCulture, WebPageResources.WebPage_SectionsNotRendered, VirtualPath, sectionsNotRendered.ToString()));
                    }
                }
                else if (!_renderedBody)
                {
                    // There are no sections defined, but RenderBody was NOT called.
                    // If a body was defined, then RenderBody should have been called.
                    throw new HttpException(String.Format(CultureInfo.CurrentCulture, WebPageResources.WebPage_RenderBodyNotCalled, VirtualPath));
                }
            }
        }

        public override void Write(HelperResult result)
        {
            WriteTo(Output, result);
        }

        public override void Write(object value)
        {
            WriteTo(Output, value);
        }

        public override void WriteLiteral(object value)
        {
            Output.Write(value);
        }

        protected internal override TextWriter GetOutputWriter()
        {
            return Output;
        }
    }
}
