// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>
/// Represents a web search tool call invocation by a hosted service.
/// </summary>
/// <remarks>
/// This content type represents when a hosted AI service invokes a web search tool
/// (e.g., OpenAI's web search, Anthropic's web search, Google Gemini's grounding).
/// It is informational only and represents the call itself, not the result.
/// </remarks>
[Experimental("MEAI001")]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class WebSearchToolCallContent : AIContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebSearchToolCallContent"/> class.
    /// </summary>
    /// <param name="callId">The tool call ID.</param>
    /// <param name="toolName">The name of the web search tool.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callId"/> or <paramref name="toolName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="callId"/> or <paramref name="toolName"/> is empty or composed entirely of whitespace.</exception>
    public WebSearchToolCallContent(string callId, string toolName = "web_search")
    {
        CallId = Throw.IfNullOrWhitespace(callId);
        ToolName = Throw.IfNullOrWhitespace(toolName);
    }

    /// <summary>
    /// Gets the tool call ID.
    /// </summary>
    public string CallId { get; }

    /// <summary>
    /// Gets the name of the tool being invoked.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// Gets or sets the search query.
    /// </summary>
    /// <remarks>
    /// This represents the query string that the service decided to search for.
    /// </remarks>
    [AllowNull]
    public string Query
    {
        get => field ?? string.Empty;
        set;
    }

    /// <summary>Gets a string representing this instance to display in the debugger.</summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"ToolCall: {ToolName} (CallId={CallId})";
}
