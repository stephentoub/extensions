// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>
/// Represents the result of a web search tool invocation by a hosted service.
/// </summary>
/// <remarks>
/// This content type captures the output from a hosted web search tool execution.
/// The <see cref="Output"/> property contains the search results,
/// which typically include <see cref="TextContent"/> for snippets or summaries,
/// <see cref="UriContent"/> or <see cref="CitationAnnotation"/> for source URLs,
/// or other <see cref="AIContent"/> types as appropriate.
/// It is informational only.
/// </remarks>
[Experimental("MEAI001")]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class WebSearchToolResultContent : AIContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebSearchToolResultContent"/> class.
    /// </summary>
    /// <param name="callId">The tool call ID that this result corresponds to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="callId"/> is empty or composed entirely of whitespace.</exception>
    public WebSearchToolResultContent(string callId)
    {
        CallId = Throw.IfNullOrWhitespace(callId);
    }

    /// <summary>
    /// Gets the tool call ID that this result corresponds to.
    /// </summary>
    public string CallId { get; }

    /// <summary>
    /// Gets or sets the output of the tool invocation.
    /// </summary>
    /// <remarks>
    /// Different tools produce different types of output. The output is represented as a collection
    /// of <see cref="AIContent"/> to allow for flexible representation of various result types such as
    /// text, data, files, or structured content.
    /// </remarks>
    public IList<AIContent>? Output { get; set; }

    /// <summary>Gets a string representing this instance to display in the debugger.</summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"ToolResult: CallId={CallId}, Output={Output?.Count ?? 0} item(s)";
}
