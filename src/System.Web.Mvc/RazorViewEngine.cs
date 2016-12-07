// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Web.Mvc
{
    public class RazorViewEngine : BuildManagerViewEngine
    {
        internal static readonly string ViewStartFileName = "_ViewStart";

        public RazorViewEngine()
            : this(null)
        {
        }

        public RazorViewEngine(IViewPageActivator viewPageActivator)
            : base(viewPageActivator)
        {
            AreaViewLocationFormats = new[]
            {
                "~/Areas/{2}/Views/{1}/{0}.cshtml",
                "~/Areas/{2}/Views/Shared/{0}.cshtml",
            };
            AreaMasterLocationFormats = new[]
            {
                "~/Areas/{2}/Views/{1}/{0}.cshtml",
                "~/Areas/{2}/Views/Shared/{0}.cshtml",
            };
            AreaPartialViewLocationFormats = new[]
            {
                "~/Areas/{2}/Views/{1}/{0}.cshtml",
                "~/Areas/{2}/Views/Shared/{0}.cshtml",
            };

            ViewLocationFormats = new[]
            {
                "~/Views/{1}/{0}.cshtml",
                "~/Views/Shared/{0}.cshtml",
            };
            MasterLocationFormats = new[]
            {
                "~/Views/{1}/{0}.cshtml",
                "~/Views/Shared/{0}.cshtml",
            };
            PartialViewLocationFormats = new[]
            {
                "~/Views/{1}/{0}.cshtml",
                "~/Views/Shared/{0}.cshtml",
            };

            FileExtensions = new[]
            {
                "cshtml",
            };
        }

        protected override IView CreatePartialView(ControllerContext controllerContext, string partialPath)
        {
            return new RazorView(controllerContext, partialPath,
                                 layoutPath: null, runViewStartPages: false, viewStartFileExtensions: FileExtensions, viewPageActivator: ViewPageActivator)
            {
                DisplayModeProvider = DisplayModeProvider
            };
        }

        protected override IView CreateView(ControllerContext controllerContext, string viewPath, string masterPath)
        {
            var view = new RazorView(controllerContext, viewPath,
                                     layoutPath: masterPath, runViewStartPages: true, viewStartFileExtensions: FileExtensions, viewPageActivator: ViewPageActivator)
            {
                DisplayModeProvider = DisplayModeProvider
            };
            return view;
        }
    }
}
