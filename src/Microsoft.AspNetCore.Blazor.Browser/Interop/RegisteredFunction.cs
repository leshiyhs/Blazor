﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using WebAssembly;

namespace Microsoft.AspNetCore.Blazor.Browser.Interop
{
    /// <summary>
    /// Provides methods for invoking preregistered JavaScript functions from .NET code.
    /// </summary>
    public static class RegisteredFunction
    {
        /// <summary>
        /// Invokes the JavaScript function registered with the specified identifier.
        /// Arguments and return values are marshalled via JSON serialization.
        /// </summary>
        /// <typeparam name="TRes">The .NET type corresponding to the function's return value type. This type must be JSON deserializable.</typeparam>
        /// <param name="identifier">The identifier used when registering the target function.</param>
        /// <param name="args">The arguments to pass, each of which must be JSON serializable.</param>
        /// <returns>The result of the function invocation.</returns>
        public static TRes Invoke<TRes>(string identifier, params object[] args)
        {
            // This is a low-perf convenience method that bypasses the need to deal with
            // .NET memory and data structures on the JS side
            var argsJson = args.Select(JsonUtil.Serialize);
            var resultJson = InvokeUnmarshalled<string>("invokeWithJsonMarshalling",
                argsJson.Prepend(identifier).ToArray());
            if (resultJson != null)
            {
                return JsonUtil.Deserialize<TRes>(resultJson);
            }

            return default;
        }

        /// <summary>
        /// Invokes the JavaScript function registered with the specified identifier.
        /// Arguments and return values are marshalled via JSON serialization.
        /// </summary>
        /// <typeparam name="TRes">The .NET type corresponding to the function's return value type. This type must be JSON deserializable.</typeparam>
        /// <param name="identifier">The identifier used when registering the target function.</param>
        /// <param name="args">The arguments to pass, each of which must be JSON serializable.</param>
        /// <returns>The result of the function invocation.</returns>
        public static Task<TRes> InvokeAsync<TRes>(string identifier, params object[] args)
        {
            var tcs = new TaskCompletionSource<TRes>();
            var argsJson = args.Select(JsonUtil.Serialize);
            var successId = Guid.NewGuid().ToString();
            var failureId = Guid.NewGuid().ToString();
            var asyncProtocol = JsonUtil.Serialize(new
            {
                Success = successId,
                Failure = failureId,
                Function = new MethodOptions
                {
                    Type = new TypeInstance
                    {
                        Assembly = typeof(JavaScriptInvoke).Assembly.FullName,
                        TypeName = typeof(JavaScriptInvoke).FullName
                    },
                    Method = new MethodInstance
                    {
                        Name = nameof(JavaScriptInvoke.InvokeTaskCallback),
                        ParameterTypes = new[]
                        {
                            new TypeInstance
                            {
                                Assembly = typeof(string).Assembly.FullName,
                                TypeName = typeof(string).FullName
                            },
                            new TypeInstance
                            {
                                Assembly = typeof(string).Assembly.FullName,
                                TypeName = typeof(string).FullName
                            }
                        }
                    }
                }
            });

            TrackedReference.Track(successId, new Action<string>(r =>
            {
                tcs.SetResult(r == null ? default : JsonUtil.Deserialize<TRes>(r));
            }));

            TrackedReference.Track(failureId, (new Action<string>(r =>
            {
                tcs.SetException(new InvalidOperationException(r));
            })));

            var resultJson = InvokeUnmarshalled<string>(
                "invokeWithJsonMarshallingAsync",
                new[] { identifier, asyncProtocol }.Concat(argsJson).ToArray());

            return tcs.Task;
        }

        /// <summary>
        /// Internal API
        /// </summary>
        /// <param name="id">The id to the callback to invoke</param>
        /// <param name="result">The result from the JavaScript promise.</param>
        public static void InvokeTaskCallback(string id, string result)
        {
            var reference = TrackedReference.Get(id);
            var function = reference.TrackedInstance as Action<string>;
            function(result);
        }

        /// <summary>
        /// Invokes the JavaScript function registered with the specified identifier.
        /// 
        /// When using this overload, all arguments will be supplied as <see cref="System.Object" />
        /// references, meaning that any reference types will be boxed. If you are passing
        /// 3 or fewer arguments, it is preferable to instead call the overload that
        /// specifies generic type arguments for each argument.
        /// </summary>
        /// <typeparam name="TRes">The .NET type corresponding to the function's return value type.</typeparam>
        /// <param name="identifier">The identifier used when registering the target function.</param>
        /// <param name="args">The arguments to pass, each of which will be supplied as a <see cref="System.Object" /> instance.</param>
        /// <returns>The result of the function invocation.</returns>
        public static TRes InvokeUnmarshalled<TRes>(string identifier, params object[] args)
        {
            var result = Runtime.BlazorInvokeJSArray<TRes>(out var exception, identifier, args);
            return exception != null
                ? throw new JavaScriptException(exception)
                : result;
        }

        /// <summary>
        /// Invokes the JavaScript function registered with the specified identifier.
        /// </summary>
        /// <typeparam name="TRes">The .NET type corresponding to the function's return value type.</typeparam>
        /// <param name="identifier">The identifier used when registering the target function.</param>
        /// <returns>The result of the function invocation.</returns>
        public static TRes InvokeUnmarshalled<TRes>(string identifier)
            => InvokeUnmarshalled<object, object, object, TRes>(identifier, null, null, null);

        /// <summary>
        /// Invokes the JavaScript function registered with the specified identifier.
        /// </summary>
        /// <typeparam name="T0">The type of the first argument.</typeparam>
        /// <typeparam name="TRes">The .NET type corresponding to the function's return value type.</typeparam>
        /// <param name="identifier">The identifier used when registering the target function.</param>
        /// <param name="arg0">The first argument.</param>
        /// <returns>The result of the function invocation.</returns>
        public static TRes InvokeUnmarshalled<T0, TRes>(string identifier, T0 arg0)
            => InvokeUnmarshalled<T0, object, object, TRes>(identifier, arg0, null, null);

        /// <summary>
        /// Invokes the JavaScript function registered with the specified identifier.
        /// </summary>
        /// <typeparam name="T0">The type of the first argument.</typeparam>
        /// <typeparam name="T1">The type of the second argument.</typeparam>
        /// <typeparam name="TRes">The .NET type corresponding to the function's return value type.</typeparam>
        /// <param name="identifier">The identifier used when registering the target function.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <returns>The result of the function invocation.</returns>
        public static TRes InvokeUnmarshalled<T0, T1, TRes>(string identifier, T0 arg0, T1 arg1)
            => InvokeUnmarshalled<T0, T1, object, TRes>(identifier, arg0, arg1, null);

        /// <summary>
        /// Invokes the JavaScript function registered with the specified identifier.
        /// </summary>
        /// <typeparam name="T0">The type of the first argument.</typeparam>
        /// <typeparam name="T1">The type of the second argument.</typeparam>
        /// <typeparam name="T2">The type of the third argument.</typeparam>
        /// <typeparam name="TRes">The .NET type corresponding to the function's return value type.</typeparam>
        /// <param name="identifier">The identifier used when registering the target function.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="arg2">The third argument.</param>
        /// <returns>The result of the function invocation.</returns>
        public static TRes InvokeUnmarshalled<T0, T1, T2, TRes>(string identifier, T0 arg0, T1 arg1, T2 arg2)
        {
            var result = Runtime.BlazorInvokeJS<T0, T1, T2, TRes>(out var exception, identifier, arg0, arg1, arg2);
            return exception != null
                ? throw new JavaScriptException(exception)
                : result;
        }
    }
}
