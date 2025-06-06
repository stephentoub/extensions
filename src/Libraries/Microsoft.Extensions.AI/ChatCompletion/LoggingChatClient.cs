﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>A delegating chat client that logs chat operations to an <see cref="ILogger"/>.</summary>
/// <remarks>
/// <para>
/// The provided implementation of <see cref="IChatClient"/> is thread-safe for concurrent use so long as the
/// <see cref="ILogger"/> employed is also thread-safe for concurrent use.
/// </para>
/// <para>
/// When the employed <see cref="ILogger"/> enables <see cref="Logging.LogLevel.Trace"/>, the contents of
/// chat messages and options are logged. These messages and options may contain sensitive application data.
/// <see cref="Logging.LogLevel.Trace"/> is disabled by default and should never be enabled in a production environment.
/// Messages and options are not logged at other logging levels.
/// </para>
/// </remarks>
public partial class LoggingChatClient : DelegatingChatClient
{
    /// <summary>An <see cref="ILogger"/> instance used for all logging.</summary>
    private readonly ILogger _logger;

    /// <summary>The <see cref="JsonSerializerOptions"/> to use for serialization of state written to the logger.</summary>
    private JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>Initializes a new instance of the <see cref="LoggingChatClient"/> class.</summary>
    /// <param name="innerClient">The underlying <see cref="IChatClient"/>.</param>
    /// <param name="logger">An <see cref="ILogger"/> instance that will be used for all logging.</param>
    public LoggingChatClient(IChatClient innerClient, ILogger logger)
        : base(innerClient)
    {
        _logger = Throw.IfNull(logger);
        _jsonSerializerOptions = AIJsonUtilities.DefaultOptions;
    }

    /// <summary>Gets or sets JSON serialization options to use when serializing logging data.</summary>
    public JsonSerializerOptions JsonSerializerOptions
    {
        get => _jsonSerializerOptions;
        set => _jsonSerializerOptions = Throw.IfNull(value);
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                LogInvokedSensitive(nameof(GetResponseAsync), AsJson(messages), AsJson(options), AsJson(this.GetService<ChatClientMetadata>()));
            }
            else
            {
                LogInvoked(nameof(GetResponseAsync));
            }
        }

        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    LogCompletedSensitive(nameof(GetResponseAsync), AsJson(response));
                }
                else
                {
                    LogCompleted(nameof(GetResponseAsync));
                }
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            LogInvocationCanceled(nameof(GetResponseAsync));
            throw;
        }
        catch (Exception ex)
        {
            LogInvocationFailed(nameof(GetResponseAsync), ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                LogInvokedSensitive(nameof(GetStreamingResponseAsync), AsJson(messages), AsJson(options), AsJson(this.GetService<ChatClientMetadata>()));
            }
            else
            {
                LogInvoked(nameof(GetStreamingResponseAsync));
            }
        }

        IAsyncEnumerator<ChatResponseUpdate> e;
        try
        {
            e = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            LogInvocationCanceled(nameof(GetStreamingResponseAsync));
            throw;
        }
        catch (Exception ex)
        {
            LogInvocationFailed(nameof(GetStreamingResponseAsync), ex);
            throw;
        }

        try
        {
            ChatResponseUpdate? update = null;
            while (true)
            {
                try
                {
                    if (!await e.MoveNextAsync())
                    {
                        break;
                    }

                    update = e.Current;
                }
                catch (OperationCanceledException)
                {
                    LogInvocationCanceled(nameof(GetStreamingResponseAsync));
                    throw;
                }
                catch (Exception ex)
                {
                    LogInvocationFailed(nameof(GetStreamingResponseAsync), ex);
                    throw;
                }

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    LogStreamingUpdateSensitive(AsJson(update));
                }

                yield return update;
            }

            LogCompleted(nameof(GetStreamingResponseAsync));
        }
        finally
        {
            await e.DisposeAsync();
        }
    }

    private string AsJson<T>(T value) => LoggingHelpers.AsJson(value, _jsonSerializerOptions);

    [LoggerMessage(LogLevel.Debug, "{MethodName} invoked.")]
    private partial void LogInvoked(string methodName);

    [LoggerMessage(LogLevel.Trace, "{MethodName} invoked: {Messages}. Options: {ChatOptions}. Metadata: {ChatClientMetadata}.")]
    private partial void LogInvokedSensitive(string methodName, string messages, string chatOptions, string chatClientMetadata);

    [LoggerMessage(LogLevel.Debug, "{MethodName} completed.")]
    private partial void LogCompleted(string methodName);

    [LoggerMessage(LogLevel.Trace, "{MethodName} completed: {ChatResponse}.")]
    private partial void LogCompletedSensitive(string methodName, string chatResponse);

    [LoggerMessage(LogLevel.Trace, "GetStreamingResponseAsync received update: {ChatResponseUpdate}")]
    private partial void LogStreamingUpdateSensitive(string chatResponseUpdate);

    [LoggerMessage(LogLevel.Debug, "{MethodName} canceled.")]
    private partial void LogInvocationCanceled(string methodName);

    [LoggerMessage(LogLevel.Error, "{MethodName} failed.")]
    private partial void LogInvocationFailed(string methodName, Exception error);
}
