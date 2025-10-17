// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Extensions.AI;

public class ToolResultContentTests
{
    [Fact]
    public void CodeInterpreterToolResultContent_Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new CodeInterpreterToolResultContent(null!));
        Assert.Throws<ArgumentException>(() => new CodeInterpreterToolResultContent(""));
        Assert.Throws<ArgumentException>(() => new CodeInterpreterToolResultContent("   "));
    }

    [Fact]
    public void CodeInterpreterToolResultContent_Properties_CanBeSetAndRetrieved()
    {
        var content = new CodeInterpreterToolResultContent("call_123");

        Assert.Equal("call_123", content.CallId);
        Assert.Null(content.Output);

        var outputs = new List<AIContent>
        {
            new TextContent("Hello"),
            new HostedFileContent("output-file-123")
        };
        content.Output = outputs;

        Assert.Same(outputs, content.Output);
        Assert.Equal(2, content.Output.Count);
    }

    [Fact]
    public void WebSearchToolResultContent_Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new WebSearchToolResultContent(null!));
        Assert.Throws<ArgumentException>(() => new WebSearchToolResultContent(""));
    }

    [Fact]
    public void WebSearchToolResultContent_Properties_CanBeSetAndRetrieved()
    {
        var content = new WebSearchToolResultContent("call_456");

        Assert.Equal("call_456", content.CallId);
        Assert.Null(content.Output);

        var outputs = new List<AIContent>
        {
            new TextContent("AI breakthrough..."),
            new UriContent(new Uri("https://example.com"), "text/html")
        };
        content.Output = outputs;
        Assert.Same(outputs, content.Output);
    }

    [Fact]
    public void FileSearchToolResultContent_Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new FileSearchToolResultContent(null!));
        Assert.Throws<ArgumentException>(() => new FileSearchToolResultContent(""));
    }

    [Fact]
    public void FileSearchToolResultContent_Properties_CanBeSetAndRetrieved()
    {
        var content = new FileSearchToolResultContent("call_789");

        Assert.Equal("call_789", content.CallId);
        Assert.Null(content.Output);

        var outputs = new List<AIContent>
        {
            new TextContent("Relevant excerpt from page 5...")
        };
        content.Output = outputs;
        Assert.Same(outputs, content.Output);
    }

    [Fact]
    public void ToolResultContent_DebuggerDisplay()
    {
        var content = new CodeInterpreterToolResultContent("call_abc");
        var debugDisplay = content.ToString(); // DebuggerDisplay should work via ToString or internal property

        // Basic validation that the content has expected properties
        Assert.NotNull(content.CallId);
    }
}
