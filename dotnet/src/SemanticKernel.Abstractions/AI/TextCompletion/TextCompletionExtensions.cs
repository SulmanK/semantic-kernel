﻿// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.AI.TextCompletion;

/// <summary>
/// Class sponsor that holds extension methods for <see cref="ITextCompletion"/> interface.
/// </summary>
public static class TextCompletionExtensions
{
    /// <summary>
    /// Get a single text completion result for the prompt and settings.
    /// </summary>
    /// <param name="textCompletion"></param>
    /// <param name="prompt">The standardized prompt input.</param>
    /// <param name="executionSettings">The AI execution settings (optional).</param>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>List of different text results generated by the remote model</returns>
    public static async Task<TextContent> GetTextContentAsync(
        this ITextCompletion textCompletion,
        string prompt,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => (await textCompletion.GetTextContentsAsync(prompt, executionSettings, kernel, cancellationToken).ConfigureAwait(false))
            .Single();
}
