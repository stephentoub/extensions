// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if !NET9_0_OR_GREATER
using System.Collections;
#endif
using System.Collections.Generic;
#if NET9_0_OR_GREATER
using System.Collections.ObjectModel;
#endif

namespace Microsoft.Extensions.AI;

/// <summary>Provides metadata about an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.</summary>
public class EmbeddingGeneratorMetadata
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingGeneratorMetadata"/> class.</summary>

    /// <param name="providerName">
    /// The name of the embedding generation provider, if applicable. Where possible, this should map to the
    /// appropriate name defined in the OpenTelemetry Semantic Conventions for Generative AI systems.
    /// </param>
    /// <param name="providerUri">The URL for accessing the embedding generation provider, if applicable.</param>
    /// <param name="modelId">The ID of the embedding generation model used, if applicable.</param>
    /// <param name="dimensions">The number of dimensions in vectors produced by this generator, if applicable.</param>
    /// <param name="supportedInputMediaTypes">A set of media types (also known as MIME types) for which the generator has stated explicit support.</param>
    public EmbeddingGeneratorMetadata(
        string? providerName = null,
        Uri? providerUri = null,
        string? modelId = null,
        int? dimensions = null,
        IEnumerable<string>? supportedInputMediaTypes = null)
    {
        ModelId = modelId;
        ProviderName = providerName;
        ProviderUri = providerUri;
        Dimensions = dimensions;

        if (supportedInputMediaTypes is not null)
        {
            HashSet<string> typesSet = new(StringComparer.OrdinalIgnoreCase);
            typesSet.UnionWith(supportedInputMediaTypes);
            SupportedInputMediaTypes = new ReadOnlySet<string>(typesSet);
        }
    }

    /// <summary>Gets the name of the embedding generation provider.</summary>
    /// <remarks>
    /// Where possible, this maps to the appropriate name defined in the
    /// OpenTelemetry Semantic Conventions for Generative AI systems.
    /// </remarks>
    public string? ProviderName { get; }

    /// <summary>Gets the URL for accessing the embedding generation provider.</summary>
    public Uri? ProviderUri { get; }

    /// <summary>Gets the ID of the model used by this embedding generation provider.</summary>
    /// <remarks>
    /// This value can be null if either the name is unknown or there are multiple possible models associated with this instance.
    /// An individual request may override this value via <see cref="EmbeddingGenerationOptions.ModelId"/>.
    /// </remarks>
    public string? ModelId { get; }

    /// <summary>Gets the number of dimensions in the embeddings produced by this instance.</summary>
    /// <remarks>
    /// This value can be null if either the number of dimensions is unknown or there are multiple possible lengths associated with this instance.
    /// An individual request may override this value via <see cref="EmbeddingGenerationOptions.Dimensions"/>.
    /// </remarks>
    public int? Dimensions { get; }

    /// <summary>
    /// Gets a read-only list of media types (also known as MIME types) the generator has declared to support as inputs.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/>, the generator has not made any declarations about supported types.
    /// If non-<see langword="null"/>, the generator supports only the listed types, and providing data
    /// that is of another type may lead to exceptions or unexpected results.
    /// </remarks>
    public ISet<string>? SupportedInputMediaTypes { get; }

#if !NET9_0_OR_GREATER
    private sealed class ReadOnlySet<T>(ISet<T> set) : ISet<T>
    {
        public bool IsReadOnly => true;

        public int Count => set.Count;

        public bool Contains(T item) => set.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => set.CopyTo(array, arrayIndex);
        public IEnumerator<T> GetEnumerator() => set.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)set).GetEnumerator();
        public bool IsProperSubsetOf(IEnumerable<T> other) => set.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => set.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => set.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => set.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => set.SetEquals(other);

        public bool Add(T item) => throw new NotSupportedException();
        void ICollection<T>.Add(T item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void ExceptWith(IEnumerable<T> other) => throw new NotSupportedException();
        public void IntersectWith(IEnumerable<T> other) => throw new NotSupportedException();
        public bool Remove(T item) => throw new NotSupportedException();
        public void SymmetricExceptWith(IEnumerable<T> other) => throw new NotSupportedException();
        public void UnionWith(IEnumerable<T> other) => throw new NotSupportedException();
    }
#endif
}
