﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Razor.Parser.SyntaxTree;

namespace System.Web.Razor.Generator
{
    public class ResolveUrlCodeGenerator : SpanCodeGenerator
    {
        public override void GenerateCode(Span target, CodeGeneratorContext context)
        {
            // Check if the host supports it
            if (String.IsNullOrEmpty(context.Host.GeneratedClassContext.ResolveUrlMethodName))
            {
                // Nope, just use the default MarkupCodeGenerator behavior
                new MarkupCodeGenerator().GenerateCode(target, context);
                return;
            }

            if (!context.Host.DesignTimeMode && String.IsNullOrEmpty(target.Content))
            {
                return;
            }

            if (!String.IsNullOrEmpty(target.Content) && !context.Host.DesignTimeMode)
            {
                string code = context.BuildCodeString(cw =>
                {
                    if (context.ExpressionRenderingMode == ExpressionRenderingMode.WriteToOutput)
                    {
                        if (!String.IsNullOrEmpty(context.TargetWriterName))
                        {
                            cw.WriteStartMethodInvoke(context.Host.GeneratedClassContext.WriteLiteralToMethodName);
                            cw.WriteSnippet(context.TargetWriterName);
                            cw.WriteParameterSeparator();
                        }
                        else
                        {
                            cw.WriteStartMethodInvoke(context.Host.GeneratedClassContext.WriteLiteralMethodName);
                        }
                    }
                    cw.WriteStartMethodInvoke(context.Host.GeneratedClassContext.ResolveUrlMethodName);
                    cw.WriteStringLiteral(target.Content);
                    cw.WriteEndMethodInvoke();

                    if (context.ExpressionRenderingMode == ExpressionRenderingMode.WriteToOutput)
                    {
                        cw.WriteEndMethodInvoke();
                        cw.WriteEndStatement();
                    }
                    else
                    {
                        cw.WriteLineContinuation();
                    }
                });
                if (context.ExpressionRenderingMode == ExpressionRenderingMode.WriteToOutput)
                {
                    context.AddStatement(code);
                }
                else
                {
                    context.BufferStatementFragment(code);
                }
            }
        }

        public override string ToString()
        {
            return "VirtualPath";
        }

        public override bool Equals(object obj)
        {
            return obj is ResolveUrlCodeGenerator;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
