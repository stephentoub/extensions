// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Extensions.AI;

public class EmbeddingGeneratorMetadataTests
{
    [Fact]
    public void Constructor_NullValues_AllowedAndRoundtrip()
    {
        EmbeddingGeneratorMetadata metadata = new(null, null, null, null, null);
        Assert.Null(metadata.ProviderName);
        Assert.Null(metadata.ProviderUri);
        Assert.Null(metadata.ModelId);
        Assert.Null(metadata.Dimensions);
        Assert.Null(metadata.SupportedInputMediaTypes);
    }

    [Fact]
    public void Constructor_Value_Roundtrips()
    {
        var uri = new Uri("https://example.com");
        EmbeddingGeneratorMetadata metadata = new("providerName", uri, "theModel", 42, ["text/plain", "text/html", "TEXT/PlAiN"]);
        Assert.Equal("providerName", metadata.ProviderName);
        Assert.Same(uri, metadata.ProviderUri);
        Assert.Equal("theModel", metadata.ModelId);
        Assert.Equal(42, metadata.Dimensions);
        Assert.Equal(["text/html", "text/plain"], metadata.SupportedInputMediaTypes?.OrderBy(s => s));
    }

    [Fact]
    public void SupportedInputMediaTypes_BehavesAsReadOnlySet()
    {
        EmbeddingGeneratorMetadata metadata = new(null, null, null, null, ["text/plain", "text/html"]);
        Assert.NotNull(metadata.SupportedInputMediaTypes);

        Assert.True(metadata.SupportedInputMediaTypes.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => metadata.SupportedInputMediaTypes.Add("text/xml"));
        Assert.Throws<NotSupportedException>(() => metadata.SupportedInputMediaTypes.Remove("text/plain"));
        Assert.Throws<NotSupportedException>(() => metadata.SupportedInputMediaTypes.Clear());

        Assert.Equal(2, metadata.SupportedInputMediaTypes.Count);
        Assert.True(metadata.SupportedInputMediaTypes.Contains("text/plain"));
        Assert.True(metadata.SupportedInputMediaTypes.Contains("TEXT/HTML"));
        Assert.False(metadata.SupportedInputMediaTypes.Contains("text/xml"));
    }
}
