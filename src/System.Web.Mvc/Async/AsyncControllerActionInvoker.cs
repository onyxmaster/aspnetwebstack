// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc.Filters;
using System.Web.Mvc.Routing;

namespace System.Web.Mvc.Async
{
    public class AsyncControllerActionInvoker : ControllerActionInvoker, IAsyncActionInvoker
    {
        private static readonly object _invokeActionTag = new object();
        private static readonly object _invokeActionMethodTag = new object();
        private static readonly object _invokeActionMethodWithFiltersTag = new object();

        protected async Task<bool> InvokeActionAsync(ControllerContext controllerContext, ActionDescriptor actionDescriptor)
        {
            if (controllerContext == null)
            {
                throw new ArgumentNullException("controllerContext");
            }

            FilterInfo filterInfo = GetFilters(controllerContext, actionDescriptor);
            ExceptionContext exceptionContext = null;
            try
            {
                AuthenticationContext authenticationContext = InvokeAuthenticationFilters(controllerContext,
                    filterInfo.AuthenticationFilters, actionDescriptor);
                if (authenticationContext.Result != null)
                {
                    // An authentication filter signaled that we should short-circuit the request. Let all
                    // authentication filters contribute to an action result (to combine authentication
                    // challenges). Then, run this action result.
                    AuthenticationChallengeContext challengeContext =
                        InvokeAuthenticationFiltersChallenge(controllerContext,
                        filterInfo.AuthenticationFilters, actionDescriptor, authenticationContext.Result);
                    await InvokeActionResultAsync(controllerContext,
                        challengeContext.Result ?? authenticationContext.Result).ConfigureAwait(false);
                }
                else
                {
                    AuthorizationContext authorizationContext = InvokeAuthorizationFilters(controllerContext, filterInfo.AuthorizationFilters, actionDescriptor);
                    if (authorizationContext.Result != null)
                    {
                        // An authorization filter signaled that we should short-circuit the request. Let all
                        // authentication filters contribute to an action result (to combine authentication
                        // challenges). Then, run this action result.
                        AuthenticationChallengeContext challengeContext =
                            InvokeAuthenticationFiltersChallenge(controllerContext,
                            filterInfo.AuthenticationFilters, actionDescriptor, authorizationContext.Result);
                        await InvokeActionResultAsync(controllerContext,
                            challengeContext.Result ?? authorizationContext.Result).ConfigureAwait(false);
                    }
                    else
                    {
                        if (controllerContext.Controller.ValidateRequest)
                        {
                            ValidateRequest(controllerContext);
                        }

                        IDictionary<string, object> parameters = GetParameterValues(controllerContext, actionDescriptor);
                        ActionExecutedContext postActionContext = 
                            await Task<ActionExecutedContext>.Factory.FromAsync(
                                delegate(AsyncCallback asyncCallback, object asyncState)
                                {
                                    return BeginInvokeActionMethodWithFilters(controllerContext, filterInfo.ActionFilters, actionDescriptor, parameters, asyncCallback, asyncState);
                                },
                                EndInvokeActionMethodWithFilters, 
                                null);
                        // The action succeeded. Let all authentication filters contribute to an action
                        // result (to combine authentication challenges; some authentication filters need
                        // to do negotiation even on a successful result). Then, run this action result.
                        AuthenticationChallengeContext challengeContext =
                            InvokeAuthenticationFiltersChallenge(controllerContext,
                            filterInfo.AuthenticationFilters, actionDescriptor,
                            postActionContext.Result);
                        await InvokeActionResultWithFiltersAsync(controllerContext, filterInfo.ResultFilters,
                            challengeContext.Result ?? postActionContext.Result).ConfigureAwait(false);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // This type of exception occurs as a result of Response.Redirect(), but we special-case so that
                // the filters don't see this as an error.
                throw;
            }
            catch (Exception ex)
            {
                // something blew up, so execute the exception filters
                exceptionContext = InvokeExceptionFilters(controllerContext, filterInfo.ExceptionFilters, ex);
                if (!exceptionContext.ExceptionHandled)
                {
                    throw;
                }
            }
            if (exceptionContext != null)
            {
                await InvokeActionResultAsync(controllerContext, exceptionContext.Result).ConfigureAwait(false);
            }
            return true;
        }

        protected virtual Task InvokeActionResultAsync(ControllerContext controllerContext, ActionResult actionResult)
        {
            return actionResult.ExecuteResultAsync(controllerContext);
        }

        protected virtual Task<ResultExecutedContext> InvokeActionResultWithFiltersAsync(ControllerContext controllerContext, IList<IResultFilter> filters, ActionResult actionResult)
        {
            ResultExecutingContext preContext = new ResultExecutingContext(controllerContext, actionResult);

            int startingFilterIndex = 0;
            return InvokeActionResultFilterRecursiveAsync(filters, startingFilterIndex, preContext, controllerContext, actionResult);
        }

        private async Task<ResultExecutedContext> InvokeActionResultFilterRecursiveAsync(IList<IResultFilter> filters, int filterIndex, ResultExecutingContext preContext, ControllerContext controllerContext, ActionResult actionResult)
        {
            // Performance-sensitive

            // For compatbility, the following behavior must be maintained
            //   The OnResultExecuting events must fire in forward order
            //   The InvokeActionResultAsync must then fire
            //   The OnResultExecuted events must fire in reverse order
            //   Earlier filters can process the results and exceptions from the handling of later filters
            // This is achieved by calling recursively and moving through the filter list forwards

            // If there are no more filters to recurse over, create the main result
            if (filterIndex > filters.Count - 1)
            {
                await InvokeActionResultAsync(controllerContext, actionResult).ConfigureAwait(false);
                return new ResultExecutedContext(controllerContext, actionResult, canceled: false, exception: null);
            }

            // Otherwise process the filters recursively
            IResultFilter filter = filters[filterIndex];
            filter.OnResultExecuting(preContext);
            if (preContext.Cancel)
            {
                return new ResultExecutedContext(preContext, preContext.Result, canceled: true, exception: null);
            }

            bool wasError = false;
            ResultExecutedContext postContext = null;
            try
            {
                // Use the filters in forward direction
                int nextFilterIndex = filterIndex + 1;
                postContext = await InvokeActionResultFilterRecursiveAsync(filters, nextFilterIndex, preContext, controllerContext, actionResult).ConfigureAwait(false);
            }
            catch (ThreadAbortException)
            {
                // This type of exception occurs as a result of Response.Redirect(), but we special-case so that
                // the filters don't see this as an error.
                postContext = new ResultExecutedContext(preContext, preContext.Result, canceled: false, exception: null);
                filter.OnResultExecuted(postContext);
                throw;
            }
            catch (Exception ex)
            {
                wasError = true;
                postContext = new ResultExecutedContext(preContext, preContext.Result, canceled: false, exception: ex);
                filter.OnResultExecuted(postContext);
                if (!postContext.ExceptionHandled)
                {
                    throw;
                }
            }
            if (!wasError)
            {
                filter.OnResultExecuted(postContext);
            }
            return postContext;
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Refactoring to reduce coupling not currently justified.")]
        [SuppressMessage("Microsoft.Web.FxCop", "MW1201:DoNotCallProblematicMethodsOnTask", Justification = "Task-based async wrapper.")]
        [SuppressMessage("Microsoft.Web.FxCop", "MW1202:DoNotUseProblematicTaskTypes", Justification = "Task-based async wrapper.")]
        public virtual IAsyncResult BeginInvokeAction(ControllerContext controllerContext, string actionName, AsyncCallback callback, object state)
        {
            if (controllerContext == null)
            {
                throw new ArgumentNullException("controllerContext");
            }

            Contract.Assert(controllerContext.RouteData != null);
            if (String.IsNullOrEmpty(actionName) && !controllerContext.RouteData.HasDirectRouteMatch())
            {
                throw Error.ParameterCannotBeNullOrEmpty("actionName");
            }

            ControllerDescriptor controllerDescriptor = GetControllerDescriptor(controllerContext);
            ActionDescriptor actionDescriptor = FindAction(controllerContext, controllerDescriptor, actionName);
            if (actionDescriptor != null)
            {
                BeginInvokeDelegate beginDelegate = delegate(AsyncCallback asyncCallback, object asyncState)
                {
                    var task = InvokeActionAsync(controllerContext, actionDescriptor);
                    var tcs = new TaskCompletionSource<bool>(asyncState);
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            tcs.TrySetException(t.Exception.InnerExceptions);
                        }
                        else if (t.IsCanceled)
                        {
                            tcs.TrySetCanceled();
                        }
                        else
                        {
                            tcs.TrySetResult(t.Result);
                        }
                        if (asyncCallback != null)
                        {
                            asyncCallback(tcs.Task);
                        }
                    }, TaskScheduler.Default);
                    return tcs.Task;
                };
                EndInvokeDelegate<bool> endDelegate = delegate(IAsyncResult asyncResult)
                {
                    return ((Task<bool>)asyncResult).Result;
                };
                return AsyncResultWrapper.Begin(callback, state, beginDelegate, endDelegate, _invokeActionTag);
            }
            else
            {
                // Notify the controller that no action was found.
                return BeginInvokeAction_ActionNotFound(callback, state);
            }
        }

        private static IAsyncResult BeginInvokeAction_ActionNotFound(AsyncCallback callback, object state)
        {
            BeginInvokeDelegate beginDelegate = BeginInvokeAction_MakeSynchronousAsyncResult;

            EndInvokeDelegate<bool> endDelegate = delegate(IAsyncResult asyncResult)
            {
                return false;
            };

            return AsyncResultWrapper.Begin(callback, state, beginDelegate, endDelegate, _invokeActionTag);
        }

        private static IAsyncResult BeginInvokeAction_MakeSynchronousAsyncResult(AsyncCallback callback, object state)
        {
            SimpleAsyncResult asyncResult = new SimpleAsyncResult(state);
            asyncResult.MarkCompleted(true /* completedSynchronously */, callback);
            return asyncResult;
        }

        protected internal virtual IAsyncResult BeginInvokeActionMethod(ControllerContext controllerContext, ActionDescriptor actionDescriptor, IDictionary<string, object> parameters, AsyncCallback callback, object state)
        {
            AsyncActionDescriptor asyncActionDescriptor = actionDescriptor as AsyncActionDescriptor;
            if (asyncActionDescriptor != null)
            {
                return BeginInvokeAsynchronousActionMethod(controllerContext, asyncActionDescriptor, parameters, callback, state);
            }
            else
            {
                return BeginInvokeSynchronousActionMethod(controllerContext, actionDescriptor, parameters, callback, state);
            }
        }

        protected internal virtual IAsyncResult BeginInvokeActionMethodWithFilters(ControllerContext controllerContext, IList<IActionFilter> filters, ActionDescriptor actionDescriptor, IDictionary<string, object> parameters, AsyncCallback callback, object state)
        {
            Func<ActionExecutedContext> endContinuation = null;

            BeginInvokeDelegate beginDelegate = delegate(AsyncCallback asyncCallback, object asyncState)
            {
                AsyncInvocationWithFilters invocation = new AsyncInvocationWithFilters(this, controllerContext, actionDescriptor, filters, parameters, asyncCallback, asyncState);

                const int StartingFilterIndex = 0;
                endContinuation = invocation.InvokeActionMethodFilterAsynchronouslyRecursive(StartingFilterIndex);

                if (invocation.InnerAsyncResult != null)
                {
                    // we're just waiting for the inner result to complete
                    return invocation.InnerAsyncResult;
                }
                else
                {
                    // something was short-circuited and the action was not called, so this was a synchronous operation
                    SimpleAsyncResult newAsyncResult = new SimpleAsyncResult(asyncState);
                    newAsyncResult.MarkCompleted(completedSynchronously: true, callback: asyncCallback);
                    return newAsyncResult;
                }
            };

            EndInvokeDelegate<ActionExecutedContext> endDelegate = delegate(IAsyncResult asyncResult)
            {
                return endContinuation();
            };

            return AsyncResultWrapper.Begin(callback, state, beginDelegate, endDelegate, _invokeActionMethodWithFiltersTag);
        }

        private IAsyncResult BeginInvokeAsynchronousActionMethod(ControllerContext controllerContext, AsyncActionDescriptor actionDescriptor, IDictionary<string, object> parameters, AsyncCallback callback, object state)
        {
            BeginInvokeDelegate beginDelegate = delegate(AsyncCallback asyncCallback, object asyncState)
            {
                return actionDescriptor.BeginExecute(controllerContext, parameters, asyncCallback, asyncState);
            };

            EndInvokeDelegate<ActionResult> endDelegate = delegate(IAsyncResult asyncResult)
            {
                object returnValue = actionDescriptor.EndExecute(asyncResult);
                ActionResult result = CreateActionResult(controllerContext, actionDescriptor, returnValue);
                return result;
            };

            return AsyncResultWrapper.Begin(callback, state, beginDelegate, endDelegate, _invokeActionMethodTag);
        }

        private IAsyncResult BeginInvokeSynchronousActionMethod(ControllerContext controllerContext, ActionDescriptor actionDescriptor, IDictionary<string, object> parameters, AsyncCallback callback, object state)
        {
            // Frequently called so ensure delegate remains static and arguments do not allocate
            EndInvokeDelegate<ActionInvocation, ActionResult> endInvokeFunc = (asyncResult, innerInvokeState) =>
                {
                    return innerInvokeState.InvokeSynchronousActionMethod();
                };
            ActionInvocation endInvokeState = new ActionInvocation(this, controllerContext, actionDescriptor, parameters);
            return AsyncResultWrapper.BeginSynchronous(callback, state, endInvokeFunc, endInvokeState, _invokeActionMethodTag);
        }

        public virtual bool EndInvokeAction(IAsyncResult asyncResult)
        {
            return AsyncResultWrapper.End<bool>(asyncResult, _invokeActionTag);
        }

        protected internal virtual ActionResult EndInvokeActionMethod(IAsyncResult asyncResult)
        {
            return AsyncResultWrapper.End<ActionResult>(asyncResult, _invokeActionMethodTag);
        }

        protected internal virtual ActionExecutedContext EndInvokeActionMethodWithFilters(IAsyncResult asyncResult)
        {
            return AsyncResultWrapper.End<ActionExecutedContext>(asyncResult, _invokeActionMethodWithFiltersTag);
        }

        protected override ControllerDescriptor GetControllerDescriptor(ControllerContext controllerContext)
        {
            // Frequently called, so ensure delegate is static
            Type controllerType = controllerContext.Controller.GetType();
            ControllerDescriptor controllerDescriptor = DescriptorCache.GetDescriptor(
                controllerType: controllerType,
                creator: ReflectedAsyncControllerDescriptor.DefaultDescriptorFactory,
                state: controllerType);
            return controllerDescriptor;
        }

        // Keep as value type to avoid per-call allocation
        private struct ActionInvocation
        {
            private readonly AsyncControllerActionInvoker _invoker;
            private readonly ControllerContext _controllerContext;
            private readonly ActionDescriptor _actionDescriptor;
            private readonly IDictionary<string, object> _parameters;

            internal ActionInvocation(AsyncControllerActionInvoker invoker, ControllerContext controllerContext, ActionDescriptor actionDescriptor, IDictionary<string, object> parameters)
            {
                Contract.Assert(invoker != null);
                Contract.Assert(controllerContext != null);
                Contract.Assert(actionDescriptor != null);
                Contract.Assert(parameters != null);

                _invoker = invoker;
                _controllerContext = controllerContext;
                _actionDescriptor = actionDescriptor;
                _parameters = parameters;
            }

            internal ActionResult InvokeSynchronousActionMethod()
            {
                return _invoker.InvokeActionMethod(_controllerContext, _actionDescriptor, _parameters);
            }
        }

        // Large and passed to many function calls, so keep as a reference type to minimize copying
        private class AsyncInvocationWithFilters
        {
            private readonly AsyncControllerActionInvoker _invoker;
            private readonly ControllerContext _controllerContext;
            private readonly ActionDescriptor _actionDescriptor;
            private readonly IList<IActionFilter> _filters;
            private readonly IDictionary<string, object> _parameters;
            private readonly AsyncCallback _asyncCallback;
            private readonly object _asyncState;
            private readonly int _filterCount;
            private readonly ActionExecutingContext _preContext;

            internal IAsyncResult InnerAsyncResult;

            internal AsyncInvocationWithFilters(AsyncControllerActionInvoker invoker, ControllerContext controllerContext, ActionDescriptor actionDescriptor, IList<IActionFilter> filters, IDictionary<string, object> parameters, AsyncCallback asyncCallback, object asyncState)
            {
                Contract.Assert(invoker != null);
                Contract.Assert(controllerContext != null);
                Contract.Assert(actionDescriptor != null);
                Contract.Assert(filters != null);
                Contract.Assert(parameters != null);

                _invoker = invoker;
                _controllerContext = controllerContext;
                _actionDescriptor = actionDescriptor;
                _filters = filters;
                _parameters = parameters;
                _asyncCallback = asyncCallback;
                _asyncState = asyncState;

                _preContext = new ActionExecutingContext(controllerContext, actionDescriptor, parameters);
                // For IList<T> it is faster to cache the count
                _filterCount = _filters.Count;
            }

            internal Func<ActionExecutedContext> InvokeActionMethodFilterAsynchronouslyRecursive(int filterIndex)
            {
                // Performance-sensitive

                // For compatability, the following behavior must be maintained
                //   The OnActionExecuting events must fire in forward order
                //   The Begin and End events must fire
                //   The OnActionExecuted events must fire in reverse order
                //   Earlier filters can process the results and exceptions from the handling of later filters
                // This is achieved by calling recursively and moving through the filter list forwards

                // If there are no more filters to recurse over, create the main result
                if (filterIndex > _filterCount - 1)
                {
                    InnerAsyncResult = _invoker.BeginInvokeActionMethod(_controllerContext, _actionDescriptor, _parameters, _asyncCallback, _asyncState);
                    return () =>
                           new ActionExecutedContext(_controllerContext, _actionDescriptor, canceled: false, exception: null)
                           {
                               Result = _invoker.EndInvokeActionMethod(InnerAsyncResult)
                           };
                }

                // Otherwise process the filters recursively
                IActionFilter filter = _filters[filterIndex];
                ActionExecutingContext preContext = _preContext;
                filter.OnActionExecuting(preContext);
                if (preContext.Result != null)
                {
                    ActionExecutedContext shortCircuitedPostContext = new ActionExecutedContext(preContext, preContext.ActionDescriptor, canceled: true, exception: null)
                    {
                        Result = preContext.Result
                    };
                    return () => shortCircuitedPostContext;
                }

                // There is a nested try / catch block here that contains much the same logic as the outer block.
                // Since an exception can occur on either side of the asynchronous invocation, we need guards on
                // on both sides. In the code below, the second side is represented by the nested delegate. This
                // is really just a parallel of the synchronous ControllerActionInvoker.InvokeActionMethodFilter()
                // method.

                try
                {
                    // Use the filters in forward direction
                    int nextFilterIndex = filterIndex + 1;
                    Func<ActionExecutedContext> continuation = InvokeActionMethodFilterAsynchronouslyRecursive(nextFilterIndex);

                    // add our own continuation, then return the new function
                    return () =>
                    {
                        ActionExecutedContext postContext;
                        bool wasError = true;

                        try
                        {
                            postContext = continuation();
                            wasError = false;
                        }
                        catch (ThreadAbortException)
                        {
                            // This type of exception occurs as a result of Response.Redirect(), but we special-case so that
                            // the filters don't see this as an error.
                            postContext = new ActionExecutedContext(preContext, preContext.ActionDescriptor, canceled: false, exception: null);
                            filter.OnActionExecuted(postContext);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            postContext = new ActionExecutedContext(preContext, preContext.ActionDescriptor, canceled: false, exception: ex);
                            filter.OnActionExecuted(postContext);
                            if (!postContext.ExceptionHandled)
                            {
                                throw;
                            }
                        }
                        if (!wasError)
                        {
                            filter.OnActionExecuted(postContext);
                        }

                        return postContext;
                    };
                }
                catch (ThreadAbortException)
                {
                    // This type of exception occurs as a result of Response.Redirect(), but we special-case so that
                    // the filters don't see this as an error.
                    ActionExecutedContext postContext = new ActionExecutedContext(preContext, preContext.ActionDescriptor, canceled: false, exception: null);
                    filter.OnActionExecuted(postContext);
                    throw;
                }
                catch (Exception ex)
                {
                    ActionExecutedContext postContext = new ActionExecutedContext(preContext, preContext.ActionDescriptor, canceled: false, exception: ex);
                    filter.OnActionExecuted(postContext);
                    if (postContext.ExceptionHandled)
                    {
                        return () => postContext;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}
