// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace System.Web.Mvc
{
    public abstract class ActionResult
    {
        public virtual void ExecuteResult(ControllerContext context)
        {
            throw new NotImplementedException(Properties.MvcResources.ActionResult_NoSynchronousImplementationAvaliable);
        }

        public virtual Task ExecuteResultAsync(ControllerContext context)
        {
            ExecuteResult(context);
            return TaskHelpers.Completed();
        }
    }
}
