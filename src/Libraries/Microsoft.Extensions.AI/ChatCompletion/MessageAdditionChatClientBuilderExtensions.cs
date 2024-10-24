// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>Provides extensions for configuring <see cref="MessageAdditionChatClient"/> instances.</summary>
public static class MessageAdditionChatClientBuilderExtensions
{
    /// <summary>
    /// Enables automatically appending the underlying client's response message onto the message list.
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/>.</param>
    /// <param name="configure">An optional callback that can be used to configure the <see cref="MessageAdditionChatClient"/> instance.</param>
    /// <returns>The <paramref name="builder"/>.</returns>
    /// <remarks>
    /// <para>
    /// If the response contains multiple choices, only the message from the first will be stored.
    /// </para>
    /// <para>
    /// <see cref="StreamingChatCompletionUpdate"/> instances are combined into a single <see cref="ChatMessage"/>.
    /// </para>
    /// </remarks>
    public static ChatClientBuilder UseMessageAddition(
        this ChatClientBuilder builder, Action<MessageAdditionChatClient>? configure = null)
    {
        _ = Throw.IfNull(builder);

        return builder.Use(innerClient =>
        {
            var chatClient = new MessageAdditionChatClient(innerClient);
            configure?.Invoke(chatClient);
            return chatClient;
        });
    }
}
