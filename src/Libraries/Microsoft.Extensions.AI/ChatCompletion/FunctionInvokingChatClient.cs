// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Shared.Diagnostics;

#pragma warning disable CA2213 // Disposable fields should be disposed

namespace Microsoft.Extensions.AI;

/// <summary>
/// A delegating chat client that invokes functions defined on <see cref="ChatOptions"/>.
/// Include this in a chat pipeline to resolve function calls automatically.
/// </summary>
/// <remarks>
/// <para>
/// When this client receives a <see cref="FunctionCallContent"/> in a chat response, it responds
/// by calling the corresponding <see cref="AIFunction"/> defined in <see cref="ChatOptions"/>,
/// producing a <see cref="FunctionResultContent"/>.
/// </para>
/// <para>
/// The provided implementation of <see cref="IChatClient"/> is thread-safe for concurrent use so long as the
/// <see cref="AIFunction"/> instances employed as part of the supplied <see cref="ChatOptions"/> are also safe.
/// The <see cref="AllowConcurrentInvocation"/> property can be used to control whether multiple function invocation
/// requests as part of the same request are invocable concurrently, but even with that set to <see langword="false"/>
/// (the default), multiple concurrent requests to this same instance and using the same tools could result in those
/// tools being used concurrently (one per request). For example, a function that accesses the HttpContext of a specific
/// ASP.NET web request should only be used as part of a single <see cref="ChatOptions"/> at a time, and only with
/// <see cref="AllowConcurrentInvocation"/> set to <see langword="false"/>, in case the inner client decided to issue multiple
/// invocation requests to that same function.
/// </para>
/// </remarks>
public partial class FunctionInvokingChatClient : DelegatingChatClient
{
    /// <summary>The <see cref="FunctionInvocationContext"/> for the current function invocation.</summary>
    private static readonly AsyncLocal<FunctionInvocationContext?> _currentContext = new();

    /// <summary>The logger to use for logging information about function invocation.</summary>
    private readonly ILogger _logger;

    /// <summary>The <see cref="ActivitySource"/> to use for telemetry.</summary>
    /// <remarks>This component does not own the instance and should not dispose it.</remarks>
    private readonly ActivitySource? _activitySource;

    /// <summary>Maximum number of roundtrips allowed to the inner client.</summary>
    private int? _maximumIterationsPerRequest;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionInvokingChatClient"/> class.
    /// </summary>
    /// <param name="innerClient">The underlying <see cref="IChatClient"/>, or the next instance in a chain of clients.</param>
    /// <param name="logger">An <see cref="ILogger"/> to use for logging information about function invocation.</param>
    public FunctionInvokingChatClient(IChatClient innerClient, ILogger? logger = null)
        : base(innerClient)
    {
        _logger = logger ?? NullLogger.Instance;
        _activitySource = innerClient.GetService<ActivitySource>();
    }

    /// <summary>
    /// Gets or sets the <see cref="FunctionInvocationContext"/> for the current function invocation.
    /// </summary>
    /// <remarks>
    /// This value flows across async calls.
    /// </remarks>
    public static FunctionInvocationContext? CurrentContext
    {
        get => _currentContext.Value;
        protected set => _currentContext.Value = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to handle exceptions that occur during function calls.
    /// </summary>
    /// <value>
    /// <see langword="false"/> if the
    /// underlying <see cref="IChatClient"/> will be instructed to give a response without invoking
    /// any further functions if a function call fails with an exception.
    /// <see langword="true"/> if the underlying <see cref="IChatClient"/> is allowed
    /// to continue attempting function calls until <see cref="MaximumIterationsPerRequest"/> is reached.
    /// The default value is <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// Changing the value of this property while the client is in use might result in inconsistencies
    /// as to whether errors are retried during an in-flight request.
    /// </remarks>
    public bool RetryOnError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether detailed exception information should be included
    /// in the chat history when calling the underlying <see cref="IChatClient"/>.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the full exception message is added to the chat history
    /// when calling the underlying <see cref="IChatClient"/>.
    /// <see langword="false"/> if a generic error message is included in the chat history.
    /// The default value is <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// Setting the value to <see langword="false"/> prevents the underlying language model from disclosing
    /// raw exception details to the end user, since it doesn't receive that information. Even in this
    /// case, the raw <see cref="Exception"/> object is available to application code by inspecting
    /// the <see cref="FunctionResultContent.Exception"/> property.
    /// </para>
    /// <para>
    /// Setting the value to <see langword="true"/> can help the underlying <see cref="IChatClient"/> bypass problems on
    /// its own, for example by retrying the function call with different arguments. However it might
    /// result in disclosing the raw exception information to external users, which can be a security
    /// concern depending on the application scenario.
    /// </para>
    /// <para>
    /// Changing the value of this property while the client is in use might result in inconsistencies
    /// as to whether detailed errors are provided during an in-flight request.
    /// </para>
    /// </remarks>
    public bool IncludeDetailedErrors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow concurrent invocation of functions.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if multiple function calls can execute in parallel.
    /// <see langword="false"/> if function calls are processed serially.
    /// The default value is <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// An individual response from the inner client might contain multiple function call requests.
    /// By default, such function calls are processed serially. Set <see cref="AllowConcurrentInvocation"/> to
    /// <see langword="true"/> to enable concurrent invocation such that multiple function calls can execute in parallel.
    /// </remarks>
    public bool AllowConcurrentInvocation { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of iterations per request.
    /// </summary>
    /// <value>
    /// The maximum number of iterations per request.
    /// The default value is <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// Each request to this <see cref="FunctionInvokingChatClient"/> might end up making
    /// multiple requests to the inner client. Each time the inner client responds with
    /// a function call request, this client might perform that invocation and send the results
    /// back to the inner client in a new request. This property limits the number of times
    /// such a roundtrip is performed. If null, there is no limit applied. If set, the value
    /// must be at least one, as it includes the initial request.
    /// </para>
    /// <para>
    /// Changing the value of this property while the client is in use might result in inconsistencies
    /// as to how many iterations are allowed for an in-flight request.
    /// </para>
    /// </remarks>
    public int? MaximumIterationsPerRequest
    {
        get => _maximumIterationsPerRequest;
        set
        {
            if (value < 1)
            {
                Throw.ArgumentOutOfRangeException(nameof(value));
            }

            _maximumIterationsPerRequest = value;
        }
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(IList<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(chatMessages);

        // A single request into this GetResponseAsync may result in multiple requests to the inner client.
        // Create an activity to group them together for better observability.
        using Activity? activity = _activitySource?.StartActivity(nameof(FunctionInvokingChatClient));

        ChatResponse? response = null;
        UsageDetails? totalUsage = null;
        IList<ChatMessage> originalChatMessages = chatMessages;
        try
        {
            for (int iteration = 0; ; iteration++)
            {
                // Make the call to the handler.
                response = await base.GetResponseAsync(chatMessages, options, cancellationToken).ConfigureAwait(false);

                // Aggregate usage data over all calls
                if (response.Usage is not null)
                {
                    totalUsage ??= new();
                    totalUsage.Add(response.Usage);
                }

                // If there are no tools to call, or for any other reason we should stop, return the response.
                if (options is null
                    || options.Tools is not { Count: > 0 }
                    || response.Choices.Count == 0
                    || (MaximumIterationsPerRequest is { } maxIterations && iteration >= maxIterations))
                {
                    break;
                }

                // If there's more than one choice, we don't know which one to add to chat history, or which
                // of their function calls to process. This should not happen except if the developer has
                // explicitly requested multiple choices. We fail aggressively to avoid cases where a developer
                // doesn't realize this and is wasting their budget requesting extra choices we'd never use.
                if (response.Choices.Count > 1)
                {
                    ThrowForMultipleChoices();
                }

                // Extract any function call contents on the first choice. If there are none, we're done.
                // We don't have any way to express a preference to use a different choice, since this
                // is a niche case especially with function calling.
                FunctionCallContent[] functionCallContents = response.Message.Contents.OfType<FunctionCallContent>().ToArray();
                if (functionCallContents.Length == 0)
                {
                    break;
                }

                // Update the chat history. If the underlying client is tracking the state, then we want to avoid re-sending
                // what we already sent as well as this response message, so create a new list to store the response message(s).
                if (response.ChatThreadId is not null)
                {
                    if (chatMessages == originalChatMessages)
                    {
                        chatMessages = [];
                    }
                    else
                    {
                        chatMessages.Clear();
                    }
                }
                else
                {
                    // Add the original response message into the history.
                    chatMessages.Add(response.Message);
                }

                // Add the responses from the function calls into the history.
                var modeAndMessages = await ProcessFunctionCallsAsync(chatMessages, options, functionCallContents, iteration, cancellationToken).ConfigureAwait(false);
                if (UpdateOptionsForMode(modeAndMessages.Mode, ref options, response.ChatThreadId))
                {
                    // Terminate
                    break;
                }
            }

            return response;
        }
        finally
        {
            if (response is not null)
            {
                response.Usage = totalUsage;
            }
        }
    }

    // Streaming implementation note:
    // The streaming implementation is a bit more complex than the non-streaming one, as the semantics around it are challenging.
    // The non-streaming implementation is able to manage the chat history: if a response contains a function call content, it adds
    // the response into the history, adds the function call result, and turns the crank; if it doesn't, it just returns the response.
    // The streaming implementation, however, can't just add in the response like this. To know whether the response contains a function
    // call, it would need to see the whole response, and if it avoids yielding messages until then, it's no longer streaming. So it
    // needs to yield the updates as they arrive, but also keep track of them.
    //
    // As is the case with non-streaming, the consumer is responsible for adding the final content to the chat history, but with streaming,
    // the consumer doesn't know which part of the streamed updates are part of the final response, which means the FunctionInvokingChatClient
    // shouldn't add anything it's yielded to the chat history, because the client may be adding that content again and it'll end up being
    // duplicated. It could hold back the function calling content, yielding everything else and adding only the function call content, but
    // that then results in things being out of order in the history, which can cause problems for systems that care (such as if thinking content
    // that resulted in function call content needs to precede that function call content). It also means that the consumer isn't aware of
    // function call content until after the whole function calling interaction has completed, which means a consumer can't use this to update
    // a UI or otherwise provide notifications of things progressing.
    //
    // The solution FunctionInvokingChatClient employs, then, is to yield everything and to not add anything to the caller's history. For
    // the purposes of continuing the conversation with the inner client, it maintains its own shadow history, which it updates with all the
    // content it gets back from the inner client along with tool results. It's then up to the client to add all of the streamed content
    // into history. However, if only the response content was yielded, that would leave function call content without corresponding function
    // result content. To address this, FunctionInvokingChatClient also yields the function result content it produces. This does have a downside,
    // which is that it muddies the water around updates having different roles (tool vs assistant). Consumers need to be aware of that, e.g.
    // using the AddRange extension method that will appropriately split the updates into multiple messages.

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IList<ChatMessage> chatMessages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _ = Throw.IfNull(chatMessages);

        // A single request into this GetStreamingResponseAsync may result in multiple requests to the inner client.
        // Create an activity to group them together for better observability.
        using Activity? activity = _activitySource?.StartActivity(nameof(FunctionInvokingChatClient));

        List<FunctionCallContent>? functionCallContents = null;
        List<ChatResponseUpdate>? updates = null;
        bool replacedChatMessages = false;
        int? choice;
        for (int iteration = 0; ; iteration++)
        {
            choice = null;
            string? chatThreadId = null;
            functionCallContents?.Clear();
            updates?.Clear();
            await foreach (var update in base.GetStreamingResponseAsync(chatMessages, options, cancellationToken).ConfigureAwait(false))
            {
                // Only one choice is allowed with automatic function calling.
                if (choice is null)
                {
                    choice = update.ChoiceIndex;
                }
                else if (choice != update.ChoiceIndex)
                {
                    ThrowForMultipleChoices();
                }

                // Track whether we got a response chat thread id. This is used to determine whether the service
                // is tracking the state of the conversation such that we shouldn't re-send what it's sent to us.
                chatThreadId ??= update.ChatThreadId;

                // If the server isn't maintaining state, track all updates so we can compose them into a message
                // we send back as part of the history.
                if (chatThreadId is null)
                {
                    (updates ??= []).Add(update);
                }

                // Separately, track the function call contents that we'll need to respond to.
                (functionCallContents ??= []).AddRange(update.Contents.OfType<FunctionCallContent>());

                yield return update;
                Activity.Current = activity; // workaround for https://github.com/dotnet/runtime/issues/47802
            }

            // If there are no tools to call, or for any other reason we should stop, return the response.
            if (options is null
                || options.Tools is not { Count: > 0 }
                || (MaximumIterationsPerRequest is { } maxIterations && iteration >= maxIterations)
                || functionCallContents is not { Count: > 0 })
            {
                break;
            }

            // Update our chat history to account for everything we've received.
            if (chatThreadId is not null)
            {
                // The inner client is telling us it already has all the state, so clear out anything we have.
                if (replacedChatMessages)
                {
                    chatMessages.Clear();
                }
                else
                {
                    chatMessages = [];
                    replacedChatMessages = true;
                }
            }
            else
            {
                // The inner client isn't tracking the state, so we need to compose a message from all the updates.
                Debug.Assert(updates?.Count > 0, "If updates were empty, we wouldn't have any function call contents.");
                if (!replacedChatMessages)
                {
                    chatMessages = [.. chatMessages];
                    replacedChatMessages = true;
                }

                chatMessages.AddRange(updates!);
            }

            // Process all of the functions, adding their results into the history.
            var modeAndMessages = await ProcessFunctionCallsAsync(chatMessages, options, functionCallContents, iteration, cancellationToken).ConfigureAwait(false);

            // Yield any created content. It's already been added into the shadow history that's now chatMessages.
            foreach (var message in modeAndMessages.MessagesAdded)
            {
                yield return new()
                {
                    AuthorName = message.AuthorName,
                    Role = message.Role,
                    Contents = message.Contents,
                    AdditionalProperties = message.AdditionalProperties,
                    ChatThreadId = chatThreadId,
                };
                Activity.Current = activity; // workaround for https://github.com/dotnet/runtime/issues/47802
            }

            if (UpdateOptionsForMode(modeAndMessages.Mode, ref options, chatThreadId))
            {
                // Terminate
                yield break;
            }
        }
    }

    /// <summary>Throws an exception when multiple choices are received.</summary>
    private static void ThrowForMultipleChoices()
    {
        // If there's more than one choice, we don't know which one to add to chat history, or which
        // of their function calls to process. This should not happen except if the developer has
        // explicitly requested multiple choices. We fail aggressively to avoid cases where a developer
        // doesn't realize this and is wasting their budget requesting extra choices we'd never use.
        throw new InvalidOperationException("Automatic function call invocation only accepts a single choice, but multiple choices were received.");
    }

    /// <summary>Updates <paramref name="options"/> for the response.</summary>
    /// <returns>true if the function calling loop should terminate; otherwise, false.</returns>
    private static bool UpdateOptionsForMode(ContinueMode mode, ref ChatOptions options, string? chatThreadId)
    {
        switch (mode)
        {
            case ContinueMode.Continue when options.ToolMode is RequiredChatToolMode:
                // We have to reset the tool mode to be non-required after the first iteration,
                // as otherwise we'll be in an infinite loop.
                options = options.Clone();
                options.ToolMode = null;
                if (chatThreadId is not null)
                {
                    options.ChatThreadId = chatThreadId;
                }

                break;

            case ContinueMode.AllowOneMoreRoundtrip:
                // The LLM gets one further chance to answer, but cannot use tools.
                options = options.Clone();
                options.Tools = null;
                options.ToolMode = null;
                if (chatThreadId is not null)
                {
                    options.ChatThreadId = chatThreadId;
                }

                break;

            case ContinueMode.Terminate:
                // Bail immediately.
                return true;

            default:
                // As with the other modes, ensure we've propagated the chat thread ID to the options.
                // We only need to clone the options if we're actually mutating it.
                if (chatThreadId is not null && options.ChatThreadId != chatThreadId)
                {
                    options = options.Clone();
                    options.ChatThreadId = chatThreadId;
                }

                break;
        }

        return false;
    }

    /// <summary>
    /// Processes the function calls in the <paramref name="functionCallContents"/> list.
    /// </summary>
    /// <param name="chatMessages">The current chat contents, inclusive of the function call contents being processed.</param>
    /// <param name="options">The options used for the response being processed.</param>
    /// <param name="functionCallContents">The function call contents representing the functions to be invoked.</param>
    /// <param name="iteration">The iteration number of how many roundtrips have been made to the inner client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ContinueMode"/> value indicating how the caller should proceed.</returns>
    private async Task<(ContinueMode Mode, IList<ChatMessage> MessagesAdded)> ProcessFunctionCallsAsync(
        IList<ChatMessage> chatMessages, ChatOptions options, IReadOnlyList<FunctionCallContent> functionCallContents, int iteration, CancellationToken cancellationToken)
    {
        // We must add a response for every tool call, regardless of whether we successfully executed it or not.
        // If we successfully execute it, we'll add the result. If we don't, we'll add an error.

        int functionCount = functionCallContents.Count;
        Debug.Assert(functionCount > 0, $"Expecteded {nameof(functionCount)} to be > 0, got {functionCount}.");

        // Process all functions. If there's more than one and concurrent invocation is enabled, do so in parallel.
        if (functionCount == 1)
        {
            FunctionInvocationResult result = await ProcessFunctionCallAsync(chatMessages, options, functionCallContents[0], iteration, 0, 1, cancellationToken).ConfigureAwait(false);
            IList<ChatMessage> added = AddResponseMessages(chatMessages, [result]);
            return (result.ContinueMode, added);
        }
        else
        {
            FunctionInvocationResult[] results;

            if (AllowConcurrentInvocation)
            {
                // Schedule the invocation of every function.
                results = await Task.WhenAll(
                    from i in Enumerable.Range(0, functionCount)
                    select Task.Run(() => ProcessFunctionCallAsync(chatMessages, options, functionCallContents[i], iteration, i, functionCount, cancellationToken))).ConfigureAwait(false);
            }
            else
            {
                // Invoke each function serially.
                results = new FunctionInvocationResult[functionCount];
                for (int i = 0; i < functionCount; i++)
                {
                    results[i] = await ProcessFunctionCallAsync(chatMessages, options, functionCallContents[i], iteration, i, functionCount, cancellationToken).ConfigureAwait(false);
                }
            }

            ContinueMode continueMode = ContinueMode.Continue;
            IList<ChatMessage> added = AddResponseMessages(chatMessages, results);
            foreach (FunctionInvocationResult fir in results)
            {
                if (fir.ContinueMode > continueMode)
                {
                    continueMode = fir.ContinueMode;
                }
            }

            return (continueMode, added);
        }
    }

    /// <summary>Processes the function call described in <paramref name="callContent"/>.</summary>
    /// <param name="chatMessages">The current chat contents, inclusive of the function call contents being processed.</param>
    /// <param name="options">The options used for the response being processed.</param>
    /// <param name="callContent">The function call content representing the function to be invoked.</param>
    /// <param name="iteration">The iteration number of how many roundtrips have been made to the inner client.</param>
    /// <param name="functionCallIndex">The 0-based index of the function being called out of <paramref name="totalFunctionCount"/> total functions.</param>
    /// <param name="totalFunctionCount">The number of function call requests made, of which this is one.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ContinueMode"/> value indicating how the caller should proceed.</returns>
    private async Task<FunctionInvocationResult> ProcessFunctionCallAsync(
        IList<ChatMessage> chatMessages, ChatOptions options, FunctionCallContent callContent,
        int iteration, int functionCallIndex, int totalFunctionCount, CancellationToken cancellationToken)
    {
        // Look up the AIFunction for the function call. If the requested function isn't available, send back an error.
        AIFunction? function = options.Tools!.OfType<AIFunction>().FirstOrDefault(t => t.Name == callContent.Name);
        if (function is null)
        {
            return new(ContinueMode.Continue, FunctionInvocationStatus.NotFound, callContent, result: null, exception: null);
        }

        FunctionInvocationContext context = new()
        {
            ChatMessages = chatMessages,
            CallContent = callContent,
            Function = function,
            Iteration = iteration,
            FunctionCallIndex = functionCallIndex,
            FunctionCount = totalFunctionCount,
        };

        object? result;
        try
        {
            result = await InvokeFunctionAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            return new(
                RetryOnError ? ContinueMode.Continue : ContinueMode.AllowOneMoreRoundtrip, // We won't allow further function calls, hence the LLM will just get one more chance to give a final answer.
                FunctionInvocationStatus.Exception,
                callContent,
                result: null,
                exception: e);
        }

        return new(
            context.Terminate ? ContinueMode.Terminate : ContinueMode.Continue,
            FunctionInvocationStatus.RanToCompletion,
            callContent,
            result,
            exception: null);
    }

    /// <summary>Represents the return value of <see cref="ProcessFunctionCallsAsync"/>, dictating how the loop should behave.</summary>
    /// <remarks>These values are ordered from least severe to most severe, and code explicitly depends on the ordering.</remarks>
    internal enum ContinueMode
    {
        /// <summary>Send back the responses and continue processing.</summary>
        Continue = 0,

        /// <summary>Send back the response but without any tools.</summary>
        AllowOneMoreRoundtrip = 1,

        /// <summary>Immediately exit the function calling loop.</summary>
        Terminate = 2,
    }

    /// <summary>Adds one or more response messages for function invocation results.</summary>
    /// <param name="chatMessages">The chat to which to add the one or more response messages.</param>
    /// <param name="results">Information about the function call invocations and results.</param>
    /// <returns>A list of all chat messages added to <paramref name="chatMessages"/>.</returns>
    protected virtual IList<ChatMessage> AddResponseMessages(IList<ChatMessage> chatMessages, ReadOnlySpan<FunctionInvocationResult> results)
    {
        _ = Throw.IfNull(chatMessages);

        var contents = new AIContent[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            contents[i] = CreateFunctionResultContent(results[i]);
        }

        ChatMessage message = new(ChatRole.Tool, contents);
        chatMessages.Add(message);
        return [message];

        FunctionResultContent CreateFunctionResultContent(FunctionInvocationResult result)
        {
            _ = Throw.IfNull(result);

            object? functionResult;
            if (result.Status == FunctionInvocationStatus.RanToCompletion)
            {
                functionResult = result.Result ?? "Success: Function completed.";
            }
            else
            {
                string message = result.Status switch
                {
                    FunctionInvocationStatus.NotFound => $"Error: Requested function \"{result.CallContent.Name}\" not found.",
                    FunctionInvocationStatus.Exception => "Error: Function failed.",
                    _ => "Error: Unknown error.",
                };

                if (IncludeDetailedErrors && result.Exception is not null)
                {
                    message = $"{message} Exception: {result.Exception.Message}";
                }

                functionResult = message;
            }

            return new FunctionResultContent(result.CallContent.CallId, functionResult) { Exception = result.Exception };
        }
    }

    /// <summary>Invokes the function asynchronously.</summary>
    /// <param name="context">
    /// The function invocation context detailing the function to be invoked and its arguments along with additional request information.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>The result of the function invocation, or <see langword="null"/> if the function invocation returned <see langword="null"/>.</returns>
    protected virtual async Task<object?> InvokeFunctionAsync(FunctionInvocationContext context, CancellationToken cancellationToken)
    {
        _ = Throw.IfNull(context);

        using Activity? activity = _activitySource?.StartActivity(context.Function.Name);

        long startingTimestamp = 0;
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            startingTimestamp = Stopwatch.GetTimestamp();
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                LogInvokingSensitive(context.Function.Name, LoggingHelpers.AsJson(context.CallContent.Arguments, context.Function.JsonSerializerOptions));
            }
            else
            {
                LogInvoking(context.Function.Name);
            }
        }

        object? result = null;
        try
        {
            CurrentContext = context;
            result = await context.Function.InvokeAsync(context.CallContent.Arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            if (activity is not null)
            {
                _ = activity.SetTag("error.type", e.GetType().FullName)
                            .SetStatus(ActivityStatusCode.Error, e.Message);
            }

            if (e is OperationCanceledException)
            {
                LogInvocationCanceled(context.Function.Name);
            }
            else
            {
                LogInvocationFailed(context.Function.Name, e);
            }

            throw;
        }
        finally
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                TimeSpan elapsed = GetElapsedTime(startingTimestamp);

                if (result is not null && _logger.IsEnabled(LogLevel.Trace))
                {
                    LogInvocationCompletedSensitive(context.Function.Name, elapsed, LoggingHelpers.AsJson(result, context.Function.JsonSerializerOptions));
                }
                else
                {
                    LogInvocationCompleted(context.Function.Name, elapsed);
                }
            }
        }

        return result;
    }

    private static TimeSpan GetElapsedTime(long startingTimestamp) =>
#if NET
        Stopwatch.GetElapsedTime(startingTimestamp);
#else
        new((long)((Stopwatch.GetTimestamp() - startingTimestamp) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
#endif

    [LoggerMessage(LogLevel.Debug, "Invoking {MethodName}.", SkipEnabledCheck = true)]
    private partial void LogInvoking(string methodName);

    [LoggerMessage(LogLevel.Trace, "Invoking {MethodName}({Arguments}).", SkipEnabledCheck = true)]
    private partial void LogInvokingSensitive(string methodName, string arguments);

    [LoggerMessage(LogLevel.Debug, "{MethodName} invocation completed. Duration: {Duration}", SkipEnabledCheck = true)]
    private partial void LogInvocationCompleted(string methodName, TimeSpan duration);

    [LoggerMessage(LogLevel.Trace, "{MethodName} invocation completed. Duration: {Duration}. Result: {Result}", SkipEnabledCheck = true)]
    private partial void LogInvocationCompletedSensitive(string methodName, TimeSpan duration, string result);

    [LoggerMessage(LogLevel.Debug, "{MethodName} invocation canceled.")]
    private partial void LogInvocationCanceled(string methodName);

    [LoggerMessage(LogLevel.Error, "{MethodName} invocation failed.")]
    private partial void LogInvocationFailed(string methodName, Exception error);

    /// <summary>Provides information about the invocation of a function call.</summary>
    public sealed class FunctionInvocationResult
    {
        internal FunctionInvocationResult(ContinueMode continueMode, FunctionInvocationStatus status, FunctionCallContent callContent, object? result, Exception? exception)
        {
            ContinueMode = continueMode;
            Status = status;
            CallContent = callContent;
            Result = result;
            Exception = exception;
        }

        /// <summary>Gets status about how the function invocation completed.</summary>
        public FunctionInvocationStatus Status { get; }

        /// <summary>Gets the function call content information associated with this invocation.</summary>
        public FunctionCallContent CallContent { get; }

        /// <summary>Gets the result of the function call.</summary>
        public object? Result { get; }

        /// <summary>Gets any exception the function call threw.</summary>
        public Exception? Exception { get; }

        /// <summary>Gets an indication for how the caller should continue the processing loop.</summary>
        internal ContinueMode ContinueMode { get; }
    }

    /// <summary>Provides error codes for when errors occur as part of the function calling loop.</summary>
    public enum FunctionInvocationStatus
    {
        /// <summary>The operation completed successfully.</summary>
        RanToCompletion,

        /// <summary>The requested function could not be found.</summary>
        NotFound,

        /// <summary>The function call failed with an exception.</summary>
        Exception,
    }
}
