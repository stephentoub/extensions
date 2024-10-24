// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>A delegating chat client that augments the list of messages with the response message from the inner client.</summary>
/// <remarks>
/// <para>
/// If the response contains multiple choices, only the message from the first will be stored.
/// </para>
/// <para>
/// <see cref="StreamingChatCompletionUpdate"/> instances from <see cref="CompleteStreamingAsync"/> are combined
/// into a single <see cref="ChatMessage"/>.
/// </para>
/// <para>
/// The provided implementation of <see cref="IChatClient"/> is thread-safe for concurrent use so long as no other client is
/// concurrently using the same list of chat messages. This client instance will mutate the message list to add to the response
/// message to it.
/// </para>
/// </remarks>
public sealed class MessageAdditionChatClient : DelegatingChatClient
{
    /// <summary>Initializes a new instance of the <see cref="MessageAdditionChatClient"/> class.</summary>.
    /// <param name="innerClient">The inner client.</param>
    public MessageAdditionChatClient(IChatClient innerClient)
        : base(innerClient)
    {
    }

    /// <summary>Gets or sets a value indicating whether to include partial responses when an exception occurs.</summary>
    /// <remarks>
    /// <para>
    /// This is only relevant for <see cref="CompleteStreamingAsync"/>, where some updates may have been received prior
    /// to receiving an exception from the underlying client. This property is ignored for <see cref="CompleteAsync"/>;
    /// if <see cref="CompleteAsync"/> fails, nothing will be added to the message list.
    /// </para>
    /// <para>
    /// The default value is <see langword="false"/>.
    /// </para>
    /// </remarks>
    public bool IncludePartialResponseOnFailure { get; set; }

    /// <summary>Gets or sets a value indicating whether to coalesce streaming updates.</summary>
    /// <remarks>
    /// <para>
    /// When <see langword="true"/>, the client will attempt to coalesce contiguous streaming updates
    /// into a single update, in order to reduce the number of individual content items that are included
    /// in manufactured <see cref="ChatMessage"/> instances. When <see langword="false"/>, the updates are
    /// kept unaltered.
    /// </para>
    /// <para>
    /// The default is <see langword="true"/>. This is only relevant for <see cref="CompleteStreamingAsync"/>;
    /// it is ignored for <see cref="CompleteAsync"/>.
    /// </para>
    /// </remarks>
    public bool CoalesceStreamingUpdates { get; set; } = true;

    /// <inheritdoc/>
    public override async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(chatMessages);

        var completion = await base.CompleteAsync(chatMessages, options, cancellationToken).ConfigureAwait(false);

        chatMessages.Add(completion.Message);

        return completion;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(chatMessages);

        bool failed = false;
        int? index = null;
        List<StreamingChatCompletionUpdate> updates = [];

        var e = base.CompleteStreamingAsync(chatMessages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            StreamingChatCompletionUpdate? update = null;
            while (true)
            {
                try
                {
                    if (!await e.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    update = e.Current;
                }
                catch
                {
                    failed = true;
                    throw;
                }

                index ??= update.ChoiceIndex;
                if (index == update.ChoiceIndex)
                {
                    updates.Add(update);
                }

                yield return update;
            }
        }
        finally
        {
            // If appropriate, combine the updates into a single ChatMessage and store that into the message list.
            if (updates.Count > 0 && (!failed || IncludePartialResponseOnFailure))
            {
                if (CoalesceStreamingUpdates)
                {
                    ChatCompletionUtilities.CoalesceTextContent(updates);
                }

                chatMessages.Add(CombineMessageUpdates(updates));
            }

            await e.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Combines <see cref="StreamingChatCompletionUpdate"/> instances into a single <see cref="ChatMessage"/>.</summary>
    /// <param name="updates">The updates to be combined.</param>
    /// <returns>The combined <see cref="ChatMessage"/>.</returns>
    /// <remarks>This implementation assumes that all updates are part of the same choice.</remarks>
    private static ChatMessage CombineMessageUpdates(List<StreamingChatCompletionUpdate> updates)
    {
        _ = Throw.IfNull(updates);

        List<AIContent> contents = [];
        ChatRole? role = null;
        string? authorName = null;
        AdditionalPropertiesDictionary? additionalProperties = null;

        foreach (var update in updates)
        {
            contents.AddRange(update.Contents);

            authorName ??= update.AuthorName;
            role ??= update.Role;

            if (update.AdditionalProperties is not null)
            {
                if (additionalProperties is null)
                {
                    additionalProperties = new(update.AdditionalProperties);
                }
                else
                {
                    foreach (var entry in update.AdditionalProperties)
                    {
                        // Use first-wins behavior to match the behavior of the other properties.
                        _ = additionalProperties.TryAdd(entry.Key, entry.Value);
                    }
                }
            }
        }

        return new()
        {
            AdditionalProperties = additionalProperties,
            AuthorName = authorName,
            Contents = contents,
            Role = role ?? ChatRole.Assistant,
        };
    }
}
