// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.AI;

public class WrapperChatClientTests
{
    [Fact]
    public void UseWrapper_InvalidArgs_Throws()
    {
        ChatClientBuilder builder = new();
        Assert.Throws<ArgumentNullException>("wrapper", () => builder.UseWrapper(null!));
    }

    [Fact]
    public async Task UseWrapper_ContextPropagated()
    {
        IList<ChatMessage> expectedMessages = [];
        ChatOptions expectedOptions = new();
        using CancellationTokenSource expectedCts = new();

        AsyncLocal<int> asyncLocal = new();

        using IChatClient innerClient = new TestChatClient
        {
            CompleteAsyncCallback = (chatMessages, options, cancellationToken) =>
            {
                Assert.Same(expectedMessages, chatMessages);
                Assert.Same(expectedOptions, options);
                Assert.Equal(expectedCts.Token, cancellationToken);
                Assert.Equal(42, asyncLocal.Value);
                return Task.FromResult(new ChatCompletion(new ChatMessage(ChatRole.Assistant, "hello")));
            },

            CompleteStreamingAsyncCallback = (chatMessages, options, cancellationToken) =>
            {
                Assert.Same(expectedMessages, chatMessages);
                Assert.Same(expectedOptions, options);
                Assert.Equal(expectedCts.Token, cancellationToken);
                Assert.Equal(42, asyncLocal.Value);
                return YieldUpdates(new StreamingChatCompletionUpdate { Text = "world" });
            },
        };

        using IChatClient client = new ChatClientBuilder()
            .UseWrapper(async (chatMessages, options, cancellationToken, next) =>
            {
                Assert.Same(expectedMessages, chatMessages);
                Assert.Same(expectedOptions, options);
                Assert.Equal(expectedCts.Token, cancellationToken);
                asyncLocal.Value = 42;
                await next(chatMessages, options, cancellationToken);
            })
            .Use(innerClient);

        Assert.Equal(0, asyncLocal.Value);
        ChatCompletion completion = await client.CompleteAsync(expectedMessages, expectedOptions, expectedCts.Token);
        Assert.Equal("hello", completion.Message.Text);

        Assert.Equal(0, asyncLocal.Value);
        completion = await client.CompleteStreamingAsync(expectedMessages, expectedOptions, expectedCts.Token).ToChatCompletionAsync();
        Assert.Equal("world", completion.Message.Text);
    }

    private static async IAsyncEnumerable<StreamingChatCompletionUpdate> YieldUpdates(params StreamingChatCompletionUpdate[] updates)
    {
        foreach (var update in updates)
        {
            await Task.Yield();
            yield return update;
        }
    }
}
