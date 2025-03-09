// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
#if NET
using System.Numerics.Tensors;
#endif
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS8425 // EnumeratorCancellation

namespace Microsoft.Extensions.AI;

internal sealed class QuantizationEmbeddingGenerator :
    IEmbeddingGenerator<string, BinaryEmbedding>
#if NET
    , IEmbeddingGenerator<string, Embedding<Half>>
#endif
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _floatService;

    public QuantizationEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> floatService)
    {
        _floatService = floatService;
    }

    void IDisposable.Dispose() => _floatService.Dispose();

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this :
        _floatService.GetService(serviceType, serviceKey);

    async IAsyncEnumerable<BinaryEmbedding> IEmbeddingGenerator<string, BinaryEmbedding>.GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options, CancellationToken cancellationToken)
    {
        var embeddings = _floatService.GenerateAsync(values, options, cancellationToken);
        await foreach (var e in embeddings.ConfigureAwait(false))
        {
            var quantized = QuantizeToBinary(e);
            quantized.Usage = e.Usage;
            quantized.AdditionalProperties = e.AdditionalProperties;
            yield return quantized;
        }
    }

    private static BinaryEmbedding QuantizeToBinary(Embedding<float> embedding)
    {
        ReadOnlySpan<float> vector = embedding.Vector.Span;

        var result = new byte[(int)Math.Ceiling(vector.Length / 8.0)];
        for (int i = 0; i < vector.Length; i++)
        {
            if (vector[i] > 0)
            {
                result[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return new(result)
        {
            CreatedAt = embedding.CreatedAt,
            ModelId = embedding.ModelId,
            AdditionalProperties = embedding.AdditionalProperties,
        };
    }

#if NET
    async IAsyncEnumerable<Embedding<Half>> IEmbeddingGenerator<string, Embedding<Half>>.GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options, CancellationToken cancellationToken)
    {
        var embeddings = _floatService.GenerateAsync(values, options, cancellationToken);
        await foreach (var e in embeddings.ConfigureAwait(false))
        {
            var quantized = QuantizeToHalf(e);
            quantized.Usage = e.Usage;
            quantized.AdditionalProperties = e.AdditionalProperties;
            yield return quantized;
        }
    }

    private static Embedding<Half> QuantizeToHalf(Embedding<float> embedding)
    {
        ReadOnlySpan<float> vector = embedding.Vector.Span;
        var result = new Half[vector.Length];
        TensorPrimitives.ConvertToHalf(vector, result);
        return new(result)
        {
            CreatedAt = embedding.CreatedAt,
            ModelId = embedding.ModelId,
            AdditionalProperties = embedding.AdditionalProperties,
        };
    }
#endif
}
