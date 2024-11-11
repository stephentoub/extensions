// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>A delegating chat client that wraps an inner client with implementations provided by delegates.</summary>
public sealed class AnonymousDelegatingChatClient : DelegatingChatClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymousDelegatingChatClient"/> class.
    /// </summary>
    /// <param name="innerClient">The inner generator.</param>
    public AnonymousDelegatingChatClient(IChatClient innerClient)
        : base(innerClient)
    {
    }

    /// <summary>Gets or sets the delegate to use as the implementation of <see cref="CompleteAsync"/>.</summary>
    /// <remarks>
    /// When non-<see langword="null"/>, this delegate is used as the implementation of <see cref="CompleteAsync"/> and
    /// will be invoked with the same arguments as the method itself, along with a reference to the inner client.
    /// When <see langword="null"/>, <see cref="CompleteAsync"/> will delegate directly to the inner client.
    /// </remarks>
    public Func<AnonymousDelegatingChatClient, IList<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatCompletion>>? CompleteAsyncFunc { get; set; }

    /// <summary>Gets or sets the delegate to use as the implementation of <see cref="CompleteStreamingAsync"/>.</summary>
    /// <remarks>
    /// When non-<see langword="null"/>, this delegate is used as the implementation of <see cref="CompleteStreamingAsync"/> and
    /// will be invoked with the same arguments as the method itself, along with a reference to the inner client.
    /// When <see langword="null"/>, <see cref="CompleteStreamingAsync"/> will delegate directly to the inner client.
    /// </remarks>
    public Func<AnonymousDelegatingChatClient, IList<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<StreamingChatCompletionUpdate>>? CompleteStreamingAsyncFunc { get; set; }

    /// <summary>Gets or sets the delegate to use as the implementation of <see cref="Metadata"/>.</summary>
    /// <remarks>
    /// When non-<see langword="null"/>, this delegate is used as the implementation of <see cref="Metadata"/> and
    /// will be invoked with a reference to the inner generator. When <see langword="null"/>, <see cref="Metadata"/>
    /// will delegate directly to the inner generator.
    /// </remarks>
    public Func<AnonymousDelegatingChatClient, ChatClientMetadata>? MetadataFunc { get; set; }

    /// <summary>Gets or sets the delegate to use as the implementation of <see cref="GetService"/>.</summary>
    /// <remarks>
    /// When non-<see langword="null"/>, this delegate is used as the implementation of <see cref="GetService"/> and
    /// will be invoked with the same arguments as the method itself, along with a reference to the inner generator.
    /// When <see langword="null"/>, <see cref="GetService"/> will delegate directly to the inner generator.
    /// </remarks>
    public Func<AnonymousDelegatingChatClient, Type, object?, object?>? GetServiceFunc { get; set; }

    /// <summary>Gets or sets the delegate to use as the implementation of <see cref="Dispose"/>.</summary>
    /// <remarks>
    /// When non-<see langword="null"/>, this delegate is used as the implementation of <see cref="Dispose"/> and
    /// will be invoked with a reference to the inner generator. When <see langword="null"/>, <see cref="Dispose"/>
    /// will delegate directly to the inner generator.
    /// </remarks>
    public Action<AnonymousDelegatingChatClient>? DisposeFunc { get; set; }

    /// <inheritdoc/>
    public override Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(chatMessages);

        return CompleteAsyncFunc is { } func ?
            func(this, chatMessages, options, cancellationToken) :
            base.CompleteAsync(chatMessages, options, cancellationToken);
    }

    /// <inheritdoc/>
    public override IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(chatMessages);

        return CompleteStreamingAsyncFunc is { } func ?
            func(this, chatMessages, options, cancellationToken) :
            base.CompleteStreamingAsync(chatMessages, options, cancellationToken);
    }

    /// <inheritdoc/>
    public override ChatClientMetadata Metadata =>
        MetadataFunc is { } func ? func(this) : base.Metadata;

    /// <inheritdoc/>
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        _ = Throw.IfNull(serviceType);

        return GetServiceFunc is { } func ?
            func(this, serviceType, serviceKey) :
            base.GetService(serviceType, serviceKey);
    }

    /// <summary>Gets the inner <see cref="IChatClient" />.</summary>
    public new IChatClient InnerClient => base.InnerClient;

    /// <summary>
    /// Provides an implementation usable for <see cref="CompleteAsyncFunc"/> that implements <see cref="IChatClient.CompleteAsync"/>
    /// via <see cref="IChatClient.CompleteStreamingAsync"/>.
    /// </summary>
    /// <param name="client">The <see cref="AnonymousDelegatingChatClient"/> instance.</param>
    /// <param name="chatMessages">The chat content to send.</param>
    /// <param name="options">The chat options to configure the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The response messages generated by the client.</returns>
    public static Task<ChatCompletion> CompleteAsyncViaCompleteStreamingAsync(
        AnonymousDelegatingChatClient client, IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(client);
        _ = Throw.IfNull(chatMessages);

        return client.InnerClient.CompleteStreamingAsync(chatMessages, options, cancellationToken).ToChatCompletionAsync(coalesceContent: true, cancellationToken);
    }

    /// <summary>
    /// Provides an implementation usable for <see cref="CompleteStreamingAsyncFunc"/> that implements <see cref="IChatClient.CompleteStreamingAsync"/>
    /// via <see cref="IChatClient.CompleteAsync"/>.
    /// </summary>
    /// <param name="client">The <see cref="AnonymousDelegatingChatClient"/> instance.</param>
    /// <param name="chatMessages">The chat content to send.</param>
    /// <param name="options">The chat options to configure the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The response messages generated by the client.</returns>
    public static IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsyncViaCompleteAsync(
        AnonymousDelegatingChatClient client, IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(client);
        _ = Throw.IfNull(chatMessages);

        return CompleteStreamingAsyncViaCompleteAsync(client, chatMessages, options, cancellationToken);

        static async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsyncViaCompleteAsync(
            AnonymousDelegatingChatClient client, IList<ChatMessage> chatMessages, ChatOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ChatCompletion completion = await client.InnerClient.CompleteAsync(chatMessages, options, cancellationToken).ConfigureAwait(false);
            foreach (var update in completion.ToStreamingChatCompletionUpdates())
            {
                yield return update;
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        Debug.Assert(disposing, "This method is only called when disposing as this type is sealed with no finalizer.");

        if (DisposeFunc is { } func)
        {
            func(this);
        }
        else
        {
            base.Dispose(disposing);
        }
    }
}
