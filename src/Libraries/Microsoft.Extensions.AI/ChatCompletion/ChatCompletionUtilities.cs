// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;

#pragma warning disable S127 // "for" loop stop conditions should be invariant

namespace Microsoft.Extensions.AI;

/// <summary>Utility methods for working with <see cref="ChatCompletion"/> and <see cref="StreamingChatCompletionUpdate"/> objects.</summary>
internal static class ChatCompletionUtilities
{
    /// <summary>Coalesces sequential <see cref="TextContent"/> updates.</summary>
    internal static void CoalesceTextContent(List<StreamingChatCompletionUpdate> updates)
    {
        StringBuilder coalescedText = new();

        // Iterate through all of the items in the list looking for contiguous items that can be coalesced.
        for (int startInclusive = 0; startInclusive < updates.Count; startInclusive++)
        {
            // If an item isn't generally coalescable, skip it.
            StreamingChatCompletionUpdate update = updates[startInclusive];
            if (update.ChoiceIndex != 0 ||
                update.Contents.Count != 1 ||
                update.Contents[0] is not TextContent textContent)
            {
                continue;
            }

            // We found a coalescable item. Look for more contiguous items that are also coalescable with it.
            int endExclusive = startInclusive + 1;
            for (; endExclusive < updates.Count; endExclusive++)
            {
                StreamingChatCompletionUpdate next = updates[endExclusive];
                if (next.ChoiceIndex != 0 ||
                    next.Contents.Count != 1 ||
                    next.Contents[0] is not TextContent ||

                    // changing role or author would be really strange, but check anyway
                    (update.Role is not null && next.Role is not null && update.Role != next.Role) ||
                    (update.AuthorName is not null && next.AuthorName is not null && update.AuthorName != next.AuthorName))
                {
                    break;
                }
            }

            // If we couldn't find anything to coalesce, there's nothing to do.
            if (endExclusive - startInclusive <= 1)
            {
                continue;
            }

            // We found a coalescable run of items. Create a new node to represent the run. We create a new one
            // rather than reappropriating one of the existing ones so as not to mutate an item already yielded.
            _ = coalescedText.Clear().Append(updates[startInclusive].Text);

            TextContent coalescedContent = new(null) // will patch the text after examining all items in the run
            {
                AdditionalProperties = textContent.AdditionalProperties?.Clone(),
            };

            StreamingChatCompletionUpdate coalesced = new()
            {
                AdditionalProperties = update.AdditionalProperties?.Clone(),
                AuthorName = update.AuthorName,
                CompletionId = update.CompletionId,
                Contents = [coalescedContent],
                CreatedAt = update.CreatedAt,
                FinishReason = update.FinishReason,
                ModelId = update.ModelId,
                Role = update.Role,

                // Explicitly don't include RawRepresentation. It's not applicable if one update ends up being used
                // to represent multiple, and it won't be serialized anyway.
            };

            // Replace the starting node with the coalesced node.
            updates[startInclusive] = coalesced;

            // Now iterate through all the rest of the updates in the run, updating the coalesced node with relevant properties,
            // and nulling out the nodes along the way. We do this rather than removing the entry in order to avoid an O(N^2) operation.
            // We'll remove all the null entries at the end of the loop, using RemoveAll to do so, which can remove all of
            // the nulls in a single O(N) pass.
            for (int i = startInclusive + 1; i < endExclusive; i++)
            {
                // Grab the next item.
                StreamingChatCompletionUpdate next = updates[i];
                updates[i] = null!;

                var nextContent = (TextContent)next.Contents[0];
                _ = coalescedText.Append(nextContent.Text);

                coalesced.AuthorName ??= next.AuthorName;
                coalesced.CompletionId ??= next.CompletionId;
                coalesced.CreatedAt ??= next.CreatedAt;
                coalesced.FinishReason ??= next.FinishReason;
                coalesced.ModelId ??= next.ModelId;
                coalesced.Role ??= next.Role;
            }

            // Complete the coalescing by patching the text of the coalesced node.
            coalesced.Text = coalescedText.ToString();

            // Jump to the last update in the run, so that when we loop around and bump ahead,
            // we're at the next update just after the run.
            startInclusive = endExclusive - 1;
        }

        // Remove all of the null slots left over from the coalescing process.
        _ = updates.RemoveAll(u => u is null);
    }
}
