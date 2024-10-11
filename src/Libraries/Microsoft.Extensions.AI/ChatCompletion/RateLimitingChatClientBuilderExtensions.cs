// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>Provides extensions for configuring <see cref="RateLimitingChatClient"/> instances.</summary>
public static class RateLimitingChatClientBuilderExtensions
{
    /// <summary>Adds rate limiting to the chat client pipeline.</summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/>.</param>
    /// <param name="rateLimiter">
    /// An optional <see cref="RateLimiter"/> with which rate limiting should be performed. If not supplied, an instance will be resolved from the service provider.
    /// </param>
    /// <param name="configure">An optional callback that can be used to configure the <see cref="RateLimitingChatClient"/> instance.</param>
    /// <returns>The <paramref name="builder"/>.</returns>
    public static ChatClientBuilder UseRateLimiting(
        this ChatClientBuilder builder, RateLimiter? rateLimiter = null, Action<RateLimitingChatClient>? configure = null)
    {
        _ = Throw.IfNull(builder);

        return builder.Use((services, innerClient) =>
        {
            rateLimiter ??= services.GetRequiredService<RateLimiter>();
            var chatClient = new RateLimitingChatClient(innerClient, rateLimiter);
            configure?.Invoke(chatClient);
            return chatClient;
        });
    }
}
