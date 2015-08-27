// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;

namespace System.Web.Mvc
{
    public interface IView
    {
        void Render(ViewContext viewContext, TextWriter writer);

        Task RenderAsync(ViewContext viewContext, TextWriter writer);
    }
}
