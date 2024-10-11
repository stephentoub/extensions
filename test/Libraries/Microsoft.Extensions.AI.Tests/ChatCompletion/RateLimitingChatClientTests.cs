// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.AI;

public class RateLimitingChatClientTests
{
    [Fact]
    public void RateLimitingChatClient_InvalidArgs_Throws()
    {
        Assert.Throws<ArgumentNullException>("innerClient", () => new RateLimitingChatClient(null!, new ConcurrencyLimiter(new() { PermitLimit = 1, QueueLimit = int.MaxValue })));
        Assert.Throws<ArgumentNullException>("rateLimiter", () => new RateLimitingChatClient(new TestChatClient(), null!));
    }

    [Fact]
    public async Task ConcurrencyLimiter_NonStreaming_OneAtATime()
    {
        using var limiter = new ConcurrencyLimiter(new() { PermitLimit = 1, QueueLimit = int.MaxValue });
        int count = 0;

        using var innerClient = new TestChatClient
        {
            CompleteAsyncCallback = async (messages, options, cancellationToken) =>
            {
                Assert.Equal(1, Interlocked.Increment(ref count));
                await Task.Yield();
                Assert.Equal(0, Interlocked.Decrement(ref count));

                return new ChatCompletion(new ChatMessage());
            },
        };

        using var client = new RateLimitingChatClient(innerClient, limiter);

        await Task.WhenAll(from i in Enumerable.Range(0, 100) select client.CompleteAsync("Hello"));
    }

    [Fact]
    public async Task ConcurrencyLimiter_Streaming_OneAtATime()
    {
        using var limiter = new ConcurrencyLimiter(new() { PermitLimit = 1, QueueLimit = int.MaxValue });
        int count = 0;

        using var innerClient = new TestChatClient
        {
            CompleteStreamingAsyncCallback = (messages, options, cancellationToken) => GetUpdates()
        };

        using var client = new RateLimitingChatClient(innerClient, limiter);

        await Task.WhenAll(
            from i in Enumerable.Range(0, 100)
            select Consume(client.CompleteStreamingAsync("Hello")));

        async Task Consume(IAsyncEnumerable<StreamingChatCompletionUpdate> updates)
        {
            await foreach (var update in updates)
            {
                await Task.Yield();
            }
        }

        async IAsyncEnumerable<StreamingChatCompletionUpdate> GetUpdates()
        {
            Assert.Equal(1, Interlocked.Increment(ref count));

            for (int i = 0; i < 10; i++)
            {
                await Task.Yield();
                yield return new StreamingChatCompletionUpdate();
            }

            Assert.Equal(0, Interlocked.Decrement(ref count));
        }
    }
}
