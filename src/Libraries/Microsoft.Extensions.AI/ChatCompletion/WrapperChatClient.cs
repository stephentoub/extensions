// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Shared.Diagnostics;

#pragma warning disable CA1031 // Do not catch general exception types

namespace Microsoft.Extensions.AI;

/// <summary>
/// Provides a wrapper around an <see cref="IChatClient"/> that allows for custom logic
/// to be executed before and after each call to the inner client.
/// </summary>
internal sealed class WrapperChatClient : DelegatingChatClient
{
    /// <summary>The delegate to invoke.</summary>
    private readonly Func<IList<ChatMessage>, ChatOptions?, CancellationToken, Func<IList<ChatMessage>, ChatOptions?, CancellationToken, Task>, Task> _wrapper;

    /// <summary>Initializes a new instance of the <see cref="WrapperChatClient"/> class.</summary>
    /// <param name="innerClient">The inner client.</param>
    /// <param name="wrapper">The delegate to invoke.</param>
    public WrapperChatClient(
        IChatClient innerClient,
        Func<IList<ChatMessage>, ChatOptions?, CancellationToken, Func<IList<ChatMessage>, ChatOptions?, CancellationToken, Task>, Task> wrapper)
        : base(innerClient)
    {
        _wrapper = Throw.IfNull(wrapper);
    }

    /// <inheritdoc />
    public override async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        ChatCompletion? completion = null;
        await _wrapper(chatMessages, options, cancellationToken, async (chatMessages, options, cancellationToken) =>
        {
            completion = await InnerClient.CompleteAsync(chatMessages, options, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        if (completion is null)
        {
            throw new InvalidOperationException("The wrapper completed successfully without producing a ChatCompletion.");
        }

        return completion;
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var updates = Channel.CreateBounded<StreamingChatCompletionUpdate>(1);

#pragma warning disable CA2016 // explicitly not forwarding the cancellation token, as we need to ensure the channel is always completed
        _ = Task.Run(async () =>
#pragma warning restore CA2016
        {
            Exception? error = null;
            try
            {
                await _wrapper(chatMessages, options, cancellationToken, async (chatMessages, options, cancellationToken) =>
                {
                    await foreach (var update in InnerClient.CompleteStreamingAsync(chatMessages, options, cancellationToken).ConfigureAwait(false))
                    {
                        await updates.Writer.WriteAsync(update, cancellationToken).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
                throw;
            }
            finally
            {
                _ = updates.Writer.TryComplete(error);
            }
        });

        return updates.Reader.ReadAllAsync(cancellationToken);
    }
}
