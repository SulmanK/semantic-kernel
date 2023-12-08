// Copyright (c) Microsoft. All rights reserved.
package com.microsoft.semantickernel.textcompletion;

import com.azure.ai.openai.OpenAIAsyncClient;
import com.microsoft.semantickernel.builders.Buildable;
import com.microsoft.semantickernel.builders.BuildersSingleton;
import com.microsoft.semantickernel.builders.SemanticKernelBuilder;
import com.microsoft.semantickernel.aiservices.AIService;
import java.util.List;
import javax.annotation.Nonnull;
import reactor.core.publisher.Flux;
import reactor.core.publisher.Mono;

/** Interface for text completion services */
public interface TextCompletion extends AIService, Buildable {

    /**
     * Creates a completion for the prompt and settings.
     *
     * @param text The prompt to complete.
     * @param requestSettings Request settings for the completion API
     * @return Text generated by the remote model
     */
    Mono<List<String>> completeAsync(
            @Nonnull String text, @Nonnull CompletionRequestSettings requestSettings);

    /**
     * Creates a completion for the prompt and settings.
     *
     * @param text The prompt to complete.
     * @param requestSettings Request settings for the completion API
     * @return Text generated by the remote model
     */
    Flux<String> completeStreamAsync(
            @Nonnull String text, @Nonnull CompletionRequestSettings requestSettings);

    static Builder builder() {
        return BuildersSingleton.INST.getInstance(Builder.class);
    }

    /**
     * Returns the default completion type for this service. This will be the type of request used
     * (streaming or non-streaming) when calls to this service are made. Defaults to {@link
     * CompletionType#STREAMING}.
     *
     * @return The default completion type for this service.
     */
    CompletionType defaultCompletionType();

    interface Builder extends SemanticKernelBuilder<TextCompletion> {

        Builder withOpenAIClient(OpenAIAsyncClient client);

        Builder withModelId(String modelId);

        /**
         * Sets the default completion type for this service. This will be the type of request used
         * (streaming or non-streaming) when calls to this service are made. Defaults to {@link
         * CompletionType#STREAMING}.
         */
        Builder withDefaultCompletionType(CompletionType completionType);
    }
}
