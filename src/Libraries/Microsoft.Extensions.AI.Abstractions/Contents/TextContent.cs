// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.AI;

/// <summary>
/// Represents text content in a chat.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class TextContent : AIContent
{
    private string? _text;
    private string? _mediaType;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextContent"/> class.
    /// </summary>
    /// <param name="text">The text content.</param>
    public TextContent(string? text)
    {
        _text = text;
    }

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    [AllowNull]
    public string Text
    {
        get => _text ?? string.Empty;
        set => _text = value;
    }

    /// <summary>Gets or sets the text media type (also known as MIME type) of the content.</summary>
    [JsonPropertyOrder(1)]
    public string? MediaType
    {
        get => _mediaType;
        set
        {
            if (value is not null)
            {
                if (!DataUriParser.IsValidMediaType(value.AsSpan(), ref value))
                {
                    Throw.ArgumentException(nameof(value), "Invalid media type.");
                }

                // Don't require a "text/" prefix in order to allow for types like "application/typescript".
            }

            _mediaType = value;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Text;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"Text = \"{Text}\"";
}
