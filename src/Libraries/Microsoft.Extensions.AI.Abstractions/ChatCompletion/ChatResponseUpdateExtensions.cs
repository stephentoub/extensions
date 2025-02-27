// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if NET
using System.Runtime.InteropServices;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Shared.Diagnostics;

#pragma warning disable S109 // Magic numbers should not be used
#pragma warning disable S127 // "for" loop stop conditions should be invariant
#pragma warning disable S1121 // Assignments should not be made from within sub-expressions
#pragma warning disable S1751 // Loops with at most one iteration should be refactored

namespace Microsoft.Extensions.AI;

/// <summary>
/// Provides extension methods for working with <see cref="ChatResponseUpdate"/> instances.
/// </summary>
public static class ChatResponseUpdateExtensions
{
    /// <summary>
    /// Converts the <see cref="ChatResponseUpdate"/> into <see cref="ChatMessage"/> instances that are
    /// then added to <paramref name="chatMessages"/>.
    /// </summary>
    /// <param name="chatMessages">The list to which the new messages should be added.</param>
    /// <param name="updates">The updates to be converted and added.</param>
    /// <param name="coalesceContent">
    /// <see langword="true"/> to attempt to coalesce contiguous <see cref="AIContent"/> items, where applicable,
    /// into a single <see cref="AIContent"/>, in order to reduce the number of individual content items that are included in
    /// the manufactured <see cref="ChatMessage"/> instances. When <see langword="false"/>, the original content items are used.
    /// The default is <see langword="true"/>.
    /// </param>
    public static void AddRange(
        this IList<ChatMessage> chatMessages,
        IEnumerable<ChatResponseUpdate> updates,
        bool coalesceContent = true)
    {
        _ = Throw.IfNull(chatMessages);
        _ = Throw.IfNull(updates);

        Dictionary<int, ChatMessage> messages = [];

        foreach (var update in updates)
        {
            if (!ProcessUpdate(update, messages, response: null, stopIfIncompatible: true))
            {
                AddMessage(chatMessages, coalesceContent, GetFirstMessage(messages));
                messages.Clear();
                _ = ProcessUpdate(update, messages, response: null, stopIfIncompatible: false);
            }
        }

        if (messages.Count > 0)
        {
            AddMessage(chatMessages, coalesceContent, GetFirstMessage(messages));
        }
    }

    /// <summary>
    /// Converts the <see cref="ChatResponseUpdate"/> into <see cref="ChatMessage"/> instances that are
    /// then added to <paramref name="chatMessages"/>.
    /// </summary>
    /// <param name="chatMessages">The list to which the new messages should be added.</param>
    /// <param name="updates">The updates to be converted and added.</param>
    /// <param name="coalesceContent">
    /// <see langword="true"/> to attempt to coalesce contiguous <see cref="AIContent"/> items, where applicable,
    /// into a single <see cref="AIContent"/>, in order to reduce the number of individual content items that are included in
    /// the manufactured <see cref="ChatMessage"/> instances. When <see langword="false"/>, the original content items are used.
    /// The default is <see langword="true"/>.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="Task"/> that represents the completion of adding items to the list.</returns>
    public static async Task AddRangeAsync(
        this IList<ChatMessage> chatMessages,
        IAsyncEnumerable<ChatResponseUpdate> updates,
        bool coalesceContent = true,
        CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(chatMessages);
        _ = Throw.IfNull(updates);

        Dictionary<int, ChatMessage> messages = [];

        await foreach (var update in updates.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!ProcessUpdate(update, messages, response: null, stopIfIncompatible: true))
            {
                AddMessage(chatMessages, coalesceContent, GetFirstMessage(messages));
                messages.Clear();
                _ = ProcessUpdate(update, messages, response: null, stopIfIncompatible: false);
            }
        }

        if (messages.Count > 0)
        {
            AddMessage(chatMessages, coalesceContent, GetFirstMessage(messages));
        }
    }

    /// <summary>Combines <see cref="ChatResponseUpdate"/> instances into a single <see cref="ChatResponse"/>.</summary>
    /// <param name="updates">The updates to be combined.</param>
    /// <param name="coalesceContent">
    /// <see langword="true"/> to attempt to coalesce contiguous <see cref="AIContent"/> items, where applicable,
    /// into a single <see cref="AIContent"/>, in order to reduce the number of individual content items that are included in
    /// the manufactured <see cref="ChatMessage"/> instances. When <see langword="false"/>, the original content items are used.
    /// The default is <see langword="true"/>.
    /// </param>
    /// <returns>The combined <see cref="ChatResponse"/>.</returns>
    /// <remarks>
    /// This is a potentially lossy conversion. Individual <see cref="ChatResponseUpdate"/> instances have properties like
    /// <see cref="ChatResponseUpdate.Role"/>, and the resulting <see cref="ChatResponse"/> has a single corresponding
    /// property. Thus, if multiple updates have different values for these properties, only one will be used. This may
    /// yield a <see cref="ChatResponse"/> that is logically inconsistent. If this is a concern, either <see cref="ToChatResponse"/>
    /// should not be used, or the updates fed in via <paramref name="updates"/> should be filtered prior to calling
    /// <see cref="ToChatResponse"/>.
    /// </remarks>
    public static ChatResponse ToChatResponse(
        this IEnumerable<ChatResponseUpdate> updates, bool coalesceContent = true)
    {
        _ = Throw.IfNull(updates);

        ChatResponse response = new([]);
        Dictionary<int, ChatMessage> messages = [];

        foreach (var update in updates)
        {
            _ = ProcessUpdate(update, messages, response, stopIfIncompatible: false);
        }

        AddMessagesToResponse(messages, response, coalesceContent);

        return response;
    }

    /// <summary>Combines <see cref="ChatResponseUpdate"/> instances into a single <see cref="ChatResponse"/>.</summary>
    /// <param name="updates">The updates to be combined.</param>
    /// <param name="coalesceContent">
    /// <see langword="true"/> to attempt to coalesce contiguous <see cref="AIContent"/> items, where applicable,
    /// into a single <see cref="AIContent"/>, in order to reduce the number of individual content items that are included in
    /// the manufactured <see cref="ChatMessage"/> instances. When <see langword="false"/>, the original content items are used.
    /// The default is <see langword="true"/>.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The combined <see cref="ChatResponse"/>.</returns>
    /// <remarks>
    /// This is a potentially lossy conversion. Individual <see cref="ChatResponseUpdate"/> instances have properties like
    /// <see cref="ChatResponseUpdate.Role"/>, and the resulting <see cref="ChatResponse"/> has a single corresponding
    /// property. Thus, if multiple updates have different values for these properties, only one will be used. This may
    /// yield a <see cref="ChatResponse"/> that is logically inconsistent. If this is a concern, either <see cref="ToChatResponse"/>
    /// should not be used, or the updates fed in via <paramref name="updates"/> should be filtered prior to calling
    /// <see cref="ToChatResponse"/>.
    /// </remarks>
    public static Task<ChatResponse> ToChatResponseAsync(
        this IAsyncEnumerable<ChatResponseUpdate> updates, bool coalesceContent = true, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(updates);

        return ToChatResponseAsync(updates, coalesceContent, cancellationToken);

        static async Task<ChatResponse> ToChatResponseAsync(
            IAsyncEnumerable<ChatResponseUpdate> updates, bool coalesceContent, CancellationToken cancellationToken)
        {
            ChatResponse response = new([]);
            Dictionary<int, ChatMessage> messages = [];

            await foreach (var update in updates.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                _ = ProcessUpdate(update, messages, response, stopIfIncompatible: false);
            }

            AddMessagesToResponse(messages, response, coalesceContent);

            return response;
        }
    }

    /// <summary>Processes the <see cref="ChatResponseUpdate"/>, incorporating its contents into <paramref name="messages"/> and <paramref name="response"/>.</summary>
    /// <param name="update">The update to process.</param>
    /// <param name="messages">The dictionary mapping <see cref="ChatResponseUpdate.ChoiceIndex"/> to the <see cref="ChatMessage"/> being built for that choice.</param>
    /// <param name="response">The <see cref="ChatResponse"/> object whose properties should be updated based on <paramref name="update"/>.</param>
    /// <param name="stopIfIncompatible">true if a change of role/author/etc. between updates should cause ProcessUpdate to cease processing and return false.</param>
    private static bool ProcessUpdate(
        ChatResponseUpdate update, Dictionary<int, ChatMessage> messages, ChatResponse? response, bool stopIfIncompatible)
    {
        if (response is not null)
        {
            response.ChatThreadId ??= update.ChatThreadId;
            response.CreatedAt ??= update.CreatedAt;
            response.FinishReason ??= update.FinishReason;
            response.ModelId ??= update.ModelId;
            response.ResponseId ??= update.ResponseId;
        }

#if NET
        ChatMessage message = CollectionsMarshal.GetValueRefOrAddDefault(messages, update.ChoiceIndex, out _) ??=
            new(default, new List<AIContent>());
#else
        if (!messages.TryGetValue(update.ChoiceIndex, out ChatMessage? message))
        {
            messages[update.ChoiceIndex] = message = new(default, new List<AIContent>());
        }
#endif

        // Update the role in the message, validating that they're compatible.
        if (update.Role is ChatRole role)
        {
            if (message.Role == default)
            {
                message.Role = role;
            }
            else if (message.Role != role)
            {
                if (stopIfIncompatible)
                {
                    return false;
                }
            }
        }

        // Same for the author name.
        if (update.AuthorName is string authorName)
        {
            if (message.AuthorName is null)
            {
                message.AuthorName = authorName;
            }
            else if (message.AuthorName != authorName)
            {
                if (stopIfIncompatible)
                {
                    return false;
                }
            }
        }

        // Incorporate all content from the update into the response.
        foreach (var content in update.Contents)
        {
            switch (content)
            {
                // Usage content is treated specially and propagated to the response's Usage.
                case UsageContent usage when response is not null:
                    (response.Usage ??= new()).Add(usage.Details);
                    break;

                default:
                    message.Contents.Add(content);
                    break;
            }
        }

        if (update.AdditionalProperties is not null)
        {
            if (message.AdditionalProperties is null)
            {
                message.AdditionalProperties = new(update.AdditionalProperties);
            }
            else
            {
                foreach (var entry in update.AdditionalProperties)
                {
                    // Use first-wins behavior to match the behavior of the other properties.
                    _ = message.AdditionalProperties.TryAdd(entry.Key, entry.Value);
                }
            }
        }

        return true;
    }

    /// <summary>Finalizes the <paramref name="response"/> object by transferring the <paramref name="messages"/> into it.</summary>
    private static void AddMessagesToResponse(Dictionary<int, ChatMessage> messages, ChatResponse response, bool coalesceContent)
    {
        if (messages.Count <= 1)
        {
            // Add the single message if there is one.
            foreach (var entry in messages)
            {
                AddMessage(response.Choices, coalesceContent, entry);
            }

            // In the vast majority case where there's only one choice, promote any additional properties
            // from the single message to the chat response, making them more discoverable and more similar
            // to how they're typically surfaced from non-streaming services.
            if (response.Choices.Count == 1 &&
                response.Choices[0].AdditionalProperties is { } messageProps)
            {
                response.Choices[0].AdditionalProperties = null;
                response.AdditionalProperties = messageProps;
            }
        }
        else
        {
            // Add all of the messages, sorted by choice index.
            foreach (var entry in messages.OrderBy(entry => entry.Key))
            {
                AddMessage(response.Choices, coalesceContent, entry);
            }

            // If there are multiple choices, we don't promote additional properties from the individual messages.
            // At a minimum, we'd want to know which choice the additional properties applied to, and if there were
            // conflicting values across the choices, it would be unclear which one should be used.
        }
    }

    /// <summary>Gets the first enumerated entry from a dictionary.</summary>
    private static KeyValuePair<TKey, TValue> GetFirstMessage<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        var e = dictionary.GetEnumerator();
        bool moved = e.MoveNext();
        Debug.Assert(moved, "Expected the dictionary to contain at least one value.");
        return e.Current;
    }

    /// <summary>Adds the message in <paramref name="entry"/> into <paramref name="chatMessages"/>.</summary>
    private static void AddMessage(IList<ChatMessage> chatMessages, bool coalesceContent, KeyValuePair<int, ChatMessage> entry)
    {
        if (entry.Value.Role == default)
        {
            entry.Value.Role = ChatRole.Assistant;
        }

        if (coalesceContent)
        {
            CoalesceTextContent((List<AIContent>)entry.Value.Contents);
        }

        chatMessages.Add(entry.Value);
    }

    /// <summary>Coalesces sequential <see cref="TextContent"/> content elements.</summary>
    private static void CoalesceTextContent(List<AIContent> contents)
    {
        StringBuilder? coalescedText = null;

        // Iterate through all of the items in the list looking for contiguous items that can be coalesced.
        int start = 0;
        while (start < contents.Count - 1)
        {
            // We need at least two TextContents in a row to be able to coalesce.
            if (contents[start] is not TextContent firstText)
            {
                start++;
                continue;
            }

            if (contents[start + 1] is not TextContent secondText)
            {
                start += 2;
                continue;
            }

            // Append the text from those nodes and continue appending subsequent TextContents until we run out.
            // We null out nodes as their text is appended so that we can later remove them all in one O(N) operation.
            coalescedText ??= new();
            _ = coalescedText.Clear().Append(firstText.Text).Append(secondText.Text);
            contents[start + 1] = null!;
            int i = start + 2;
            for (; i < contents.Count && contents[i] is TextContent next; i++)
            {
                _ = coalescedText.Append(next.Text);
                contents[i] = null!;
            }

            // Store the replacement node.
            contents[start] = new TextContent(coalescedText.ToString())
            {
                // We inherit the properties of the first text node. We don't currently propagate additional
                // properties from the subsequent nodes. If we ever need to, we can add that here.
                AdditionalProperties = firstText.AdditionalProperties?.Clone(),
            };

            start = i;
        }

        // Remove all of the null slots left over from the coalescing process.
        _ = contents.RemoveAll(u => u is null);
    }
}
