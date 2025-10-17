// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>
/// Represents a file search tool call invocation by a hosted service.
/// </summary>
/// <remarks>
/// This content type represents when a hosted AI service invokes a file search tool
/// (e.g., OpenAI's file search, Azure's file search) to search through uploaded documents
/// or vector stores. It is informational only and represents the call itself, not the result.
/// </remarks>
[Experimental("MEAI001")]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class FileSearchToolCallContent : AIContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileSearchToolCallContent"/> class.
    /// </summary>
    /// <param name="callId">The tool call ID.</param>
    /// <param name="toolName">The name of the file search tool.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callId"/> or <paramref name="toolName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="callId"/> or <paramref name="toolName"/> is empty or composed entirely of whitespace.</exception>
    public FileSearchToolCallContent(string callId, string toolName = "file_search")
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
    /// This represents the query string that the service used to search through files.
    /// </remarks>
    public string? Query { get; set; }

    /// <summary>
    /// Gets or sets the inputs to the file search tool.
    /// </summary>
    /// <remarks>
    /// Inputs can include <see cref="HostedFileContent"/> for specific files to search,
    /// <see cref="HostedVectorStoreContent"/> for vector stores to query,
    /// or other <see cref="AIContent"/> types as supported by the service.
    /// </remarks>
    public IList<AIContent>? Inputs { get; set; }

    /// <summary>Gets a string representing this instance to display in the debugger.</summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"ToolCall: {ToolName} (CallId={CallId})";
}
