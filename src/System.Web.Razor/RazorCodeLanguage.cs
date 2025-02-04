﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web.Razor.Generator;
using System.Web.Razor.Parser;

namespace System.Web.Razor
{
    /// <summary>
    /// Represents a code language in Razor.
    /// </summary>
    public abstract class RazorCodeLanguage
    {
        private static IDictionary<string, RazorCodeLanguage> _services = new Dictionary<string, RazorCodeLanguage>(StringComparer.OrdinalIgnoreCase)
        {
            { "cshtml", new CSharpRazorCodeLanguage() },
        };

        /// <summary>
        /// Gets the list of registered languages mapped to file extensions (without a ".")
        /// </summary>
        public static IDictionary<string, RazorCodeLanguage> Languages
        {
            get { return _services; }
        }

        /// <summary>
        /// The name of the language (for use in System.Web.Compilation.BuildProvider.GetDefaultCompilerTypeForLanguage)
        /// </summary>
        public abstract string LanguageName { get; }

        /// <summary>
        /// The type of the CodeDOM provider for this language
        /// </summary>
        public abstract Type CodeDomProviderType { get; }

        /// <summary>
        /// Gets the RazorCodeLanguage registered for the specified file extension
        /// </summary>
        /// <param name="fileExtension">The extension, with or without a "."</param>
        /// <returns>The language registered for that extension</returns>
        public static RazorCodeLanguage GetLanguageByExtension(string fileExtension)
        {
            RazorCodeLanguage service = null;
            Languages.TryGetValue(fileExtension.TrimStart('.'), out service);
            return service;
        }

        /// <summary>
        /// Constructs the code parser.  Must return a new instance on EVERY call to ensure thread-safety
        /// </summary>
        public abstract ParserBase CreateCodeParser();

        /// <summary>
        /// Constructs the code generator.  Must return a new instance on EVERY call to ensure thread-safety
        /// </summary>
        public abstract RazorCodeGenerator CreateCodeGenerator(string className, string rootNamespaceName, string sourceFileName, RazorEngineHost host);
    }
}
