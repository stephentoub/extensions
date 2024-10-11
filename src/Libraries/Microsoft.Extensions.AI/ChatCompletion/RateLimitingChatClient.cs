// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>A delegating chat client that rate limits access to the inner client via a <see cref="RateLimiter"/>.</summary>
public sealed class RateLimitingChatClient : DelegatingChatClient
{
    /// <summary>A <see cref="RateLimiter"/> instance used for all rate limiting.</summary>
    private readonly RateLimiter _rateLimiter;

    /// <summary>Initializes a new instance of the <see cref="RateLimitingChatClient"/> class.</summary>
    /// <param name="innerClient">The underlying <see cref="IChatClient"/>.</param>
    /// <param name="rateLimiter">A <see cref="RateLimiter"/> instance that will be used for all rate limiting.</param>
    public RateLimitingChatClient(IChatClient innerClient, RateLimiter rateLimiter)
        : base(innerClient)
    {
        _rateLimiter = Throw.IfNull(rateLimiter);
    }

    /// <inheritdoc/>
    public override async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var lease = await _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken).ConfigureAwait(false);
        ThrowIfNotAcquired(lease);

        return await base.CompleteAsync(chatMessages, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var lease = await _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken).ConfigureAwait(false);
        ThrowIfNotAcquired(lease);

        await foreach (var update in base.CompleteStreamingAsync(chatMessages, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rateLimiter.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Throws an exception if unable to acquire a lease on the rate limiter.</summary>
    private static void ThrowIfNotAcquired(RateLimitLease lease)
    {
        if (!lease.IsAcquired)
        {
            Throw();
        }

        static void Throw() => throw new InvalidOperationException("Unable to acquire lease.");
    }
}
