// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#pragma warning disable CS8425 // EnumeratorCancellation

namespace Microsoft.Extensions.AI;

public class UseDelegateEmbeddingGeneratorTests
{
    [Fact]
    public void InvalidArgs_Throws()
    {
        using var generator = new TestEmbeddingGenerator();
        EmbeddingGeneratorBuilder<string, Embedding<float>> builder = new(generator);

        Assert.Throws<ArgumentNullException>("generateFunc", () =>
            builder.Use(
                (Func<IEnumerable<string>, EmbeddingGenerationOptions?,
                IEmbeddingGenerator<string, Embedding<float>>, CancellationToken, IAsyncEnumerable<Embedding<float>>>)null!));
    }

    [Fact]
    public async Task GenerateFunc_ContextPropagated()
    {
        List<Embedding<float>> innerEmbeddings = [new(new[] { 1f, 2f })];
        List<Embedding<float>> middleEmbeddings = [new(new[] { 3f, 4f })];
        IList<string> expectedValues = ["hello"];
        EmbeddingGenerationOptions expectedOptions = new();
        using CancellationTokenSource expectedCts = new();
        AsyncLocal<int> asyncLocal = new();

        using IEmbeddingGenerator<string, Embedding<float>> innerGenerator = new TestEmbeddingGenerator
        {
            GenerateAsyncCallback = (values, options, cancellationToken) =>
            {
                Assert.Same(expectedValues, values);
                Assert.Same(expectedOptions, options);
                Assert.Equal(expectedCts.Token, cancellationToken);
                Assert.Equal(42, asyncLocal.Value);
                return innerEmbeddings.ToAsyncEnumerable();
            },
        };

        using IEmbeddingGenerator<string, Embedding<float>> generator = new EmbeddingGeneratorBuilder<string, Embedding<float>>(innerGenerator)
            .Use((values, options, innerGenerator, cancellationToken) =>
            {
                Assert.Same(expectedValues, values);
                Assert.Same(expectedOptions, options);
                Assert.Equal(expectedCts.Token, cancellationToken);

                return InnerGenerateAsync(values, options, cancellationToken);

                async IAsyncEnumerable<Embedding<float>> InnerGenerateAsync(
                    IEnumerable<string> values, EmbeddingGenerationOptions? options, CancellationToken cancellationToken)
                {
                    asyncLocal.Value = 42;

                    await foreach (var e in innerGenerator.GenerateAsync(values, options, cancellationToken))
                    {
                        yield return e;
                    }

                    foreach (var e in middleEmbeddings)
                    {
                        yield return e;
                    }
                }
            })
            .Build();

        Assert.Equal(0, asyncLocal.Value);

        var actual = await generator.GenerateAsync(expectedValues, expectedOptions, expectedCts.Token).ToListAsync();
        Assert.Equal([.. innerEmbeddings, .. middleEmbeddings], actual);
    }
}
