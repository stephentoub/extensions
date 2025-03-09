// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace System.Linq;

// Replace with System.Linq.AsyncEnumerable once it's available to this repo

internal static class AsyncEnumerableHelpers
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> values)
    {
        List<T> result = new();
        await foreach (var v in values)
        {
            result.Add(v);
        }

        return result;
    }

    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> values)
    {
        foreach (T value in values)
        {
            yield return value;
        }
    }

    public static async IAsyncEnumerable<T> Append<T>(this IAsyncEnumerable<T> values, T value)
    {
        await foreach (var v in values)
        {
            yield return v;
        }

        yield return value;
    }
}
