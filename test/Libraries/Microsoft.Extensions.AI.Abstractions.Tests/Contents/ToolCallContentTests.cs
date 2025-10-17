// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Extensions.AI;

public class ToolCallContentTests
{
    [Fact]
    public void CodeInterpreterToolCallContent_Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new CodeInterpreterToolCallContent(null!));
        Assert.Throws<ArgumentException>(() => new CodeInterpreterToolCallContent(""));
        Assert.Throws<ArgumentException>(() => new CodeInterpreterToolCallContent("   "));
    }

    [Fact]
    public void CodeInterpreterToolCallContent_Properties_CanBeSetAndRetrieved()
    {
        var content = new CodeInterpreterToolCallContent("call_123");

        Assert.Equal("call_123", content.CallId);
        Assert.Equal("code_interpreter", content.ToolName);
        Assert.Null(content.Inputs);

        var inputs = new List<AIContent>
        {
            new TextContent("print('Hello')"),
            new HostedFileContent("file-123")
        };
        content.Inputs = inputs;

        Assert.Same(inputs, content.Inputs);
        Assert.Equal(2, content.Inputs.Count);
    }

    [Fact]
    public void CodeInterpreterToolCallContent_CustomToolName()
    {
        var content = new CodeInterpreterToolCallContent("call_456", "custom_code");
        Assert.Equal("custom_code", content.ToolName);
    }

    [Fact]
    public void WebSearchToolCallContent_Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new WebSearchToolCallContent(null!));
        Assert.Throws<ArgumentException>(() => new WebSearchToolCallContent(""));
    }

    [Fact]
    public void WebSearchToolCallContent_Properties_CanBeSetAndRetrieved()
    {
        var content = new WebSearchToolCallContent("call_789");

        Assert.Equal("call_789", content.CallId);
        Assert.Equal("web_search", content.ToolName);
        Assert.Equal(string.Empty, content.Query);

        content.Query = "latest AI news";
        Assert.Equal("latest AI news", content.Query);

        content.Query = null;
        Assert.Equal(string.Empty, content.Query);
    }

    [Fact]
    public void FileSearchToolCallContent_Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new FileSearchToolCallContent(null!));
        Assert.Throws<ArgumentException>(() => new FileSearchToolCallContent(""));
    }

    [Fact]
    public void FileSearchToolCallContent_Properties_CanBeSetAndRetrieved()
    {
        var content = new FileSearchToolCallContent("call_abc");

        Assert.Equal("call_abc", content.CallId);
        Assert.Equal("file_search", content.ToolName);
        Assert.Null(content.Query);
        Assert.Null(content.Inputs);

        content.Query = "quarterly report";
        Assert.Equal("quarterly report", content.Query);

        var inputs = new List<AIContent>
        {
            new HostedFileContent("file-456"),
            new HostedVectorStoreContent("vs-789")
        };
        content.Inputs = inputs;
        Assert.Same(inputs, content.Inputs);
    }
}
