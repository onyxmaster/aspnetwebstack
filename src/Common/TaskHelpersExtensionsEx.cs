// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Reflection;

namespace System.Threading.Tasks
{
    /// <summary>
    /// Extra helpers for safely using Task libraries. 
    /// </summary>
    internal static class TaskHelpersExtensionsEx
    {
        internal static bool IsCompletedSynchronously(this Task task)
        {
            if (_Task_Options == null || !task.IsCompleted)
            {
                return false;
            }
            const TaskCreationOptions InternalTaskOptions_DoNotDispose = (TaskCreationOptions)16384;
            var doNotDispose = _Task_Options(task) == InternalTaskOptions_DoNotDispose;
            return doNotDispose;
        }

        private static readonly Func<Task, TaskCreationOptions> _Task_Options = ReflectionHelpers.CreatePropertyGetter<Task, TaskCreationOptions>("Options");
    }
}
