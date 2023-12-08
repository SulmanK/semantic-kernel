﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextGeneration;
using Moq;
using Xunit;

// ReSharper disable StringLiteralTypo

namespace SemanticKernel.UnitTests.Functions;

public class KernelFunctionFromPromptTests
{
    [Fact]
    public void ItProvidesAccessToFunctionsViaFunctionCollection()
    {
        // Arrange
        var factory = new Mock<Func<IServiceProvider, ITextGenerationService>>();
        KernelBuilder builder = new();
        builder.Services.AddSingleton(factory.Object);
        Kernel kernel = builder.Build();

        kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions("jk", functions: new[] { kernel.CreateFunctionFromPrompt(promptTemplate: "Tell me a joke", functionName: "joker", description: "Nice fun") }));

        // Act & Assert - 3 functions, var name is not case sensitive
        Assert.True(kernel.Plugins.TryGetFunction("jk", "joker", out _));
        Assert.True(kernel.Plugins.TryGetFunction("JK", "JOKER", out _));
    }

    [Theory]
    [InlineData(null, "Assistant is a large language model.")]
    [InlineData("My Chat Prompt", "My Chat Prompt")]
    public async Task ItUsesChatSystemPromptWhenProvidedAsync(string providedSystemChatPrompt, string expectedSystemChatPrompt)
    {
        // Arrange
        var mockTextGeneration = new Mock<ITextGenerationService>();
        var fakeTextContent = new TextContent("llmResult");

        mockTextGeneration.Setup(c => c.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { fakeTextContent });

        KernelBuilder builder = new();
        builder.Services.AddKeyedSingleton("x", mockTextGeneration.Object);
        Kernel kernel = builder.Build();

        var promptConfig = new PromptTemplateConfig();
        promptConfig.Template = "template";
        promptConfig.ExecutionSettings.Add(new OpenAIPromptExecutionSettings()
        {
            ChatSystemPrompt = providedSystemChatPrompt
        });

        var func = kernel.CreateFunctionFromPrompt(promptConfig);

        // Act
        await kernel.InvokeAsync(func);

        // Assert
        mockTextGeneration.Verify(a => a.GetTextContentsAsync("template", It.Is<OpenAIPromptExecutionSettings>(c => c.ChatSystemPrompt == expectedSystemChatPrompt), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ItUsesServiceIdWhenProvidedAsync()
    {
        // Arrange
        var mockTextGeneration1 = new Mock<ITextGenerationService>();
        var mockTextGeneration2 = new Mock<ITextGenerationService>();
        var fakeTextContent = new TextContent("llmResult");

        mockTextGeneration1.Setup(c => c.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { fakeTextContent });
        mockTextGeneration2.Setup(c => c.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { fakeTextContent });

        KernelBuilder builder = new();
        builder.Services.AddKeyedSingleton("service1", mockTextGeneration1.Object);
        builder.Services.AddKeyedSingleton("service2", mockTextGeneration2.Object);
        Kernel kernel = builder.Build();

        var promptConfig = new PromptTemplateConfig();
        promptConfig.Template = "template";
        promptConfig.ExecutionSettings.Add(new PromptExecutionSettings() { ServiceId = "service1" });
        var func = kernel.CreateFunctionFromPrompt(promptConfig);

        // Act
        await kernel.InvokeAsync(func);

        // Assert
        mockTextGeneration1.Verify(a => a.GetTextContentsAsync("template", It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Once());
        mockTextGeneration2.Verify(a => a.GetTextContentsAsync("template", It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ItFailsIfInvalidServiceIdIsProvidedAsync()
    {
        // Arrange
        var mockTextGeneration1 = new Mock<ITextGenerationService>();
        var mockTextGeneration2 = new Mock<ITextGenerationService>();

        KernelBuilder builder = new();
        builder.Services.AddKeyedSingleton("service1", mockTextGeneration1.Object);
        builder.Services.AddKeyedSingleton("service2", mockTextGeneration2.Object);
        Kernel kernel = builder.Build();

        var promptConfig = new PromptTemplateConfig();
        promptConfig.Template = "template";
        promptConfig.ExecutionSettings.Add(new PromptExecutionSettings() { ServiceId = "service3" });
        var func = kernel.CreateFunctionFromPrompt(promptConfig);

        // Act
        var exception = await Assert.ThrowsAsync<KernelException>(() => kernel.InvokeAsync(func));

        // Assert
        Assert.Equal("Required service of type Microsoft.SemanticKernel.TextGeneration.ITextGenerationService not registered. Expected serviceIds: service3.", exception.Message);
    }

    [Fact]
    public async Task ItParsesStandardizedPromptWhenServiceIsChatCompletionAsync()
    {
        var fakeService = new FakeChatAsTextService();
        KernelBuilder builder = new();
        builder.Services.AddTransient<ITextGenerationService>((sp) => fakeService);
        Kernel kernel = builder.Build();

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt(@"
            <message role=""system"">You are a helpful assistant.</message>
            <message role=""user"">How many 20 cents can I get from 1 dollar?</message>
        ");

        // Act + Assert
        await kernel.InvokeAsync(function);

        Assert.NotNull(fakeService.ChatHistory);
        Assert.Equal(2, fakeService.ChatHistory.Count);
        Assert.Equal("You are a helpful assistant.", fakeService.ChatHistory[0].Content);
        Assert.Equal("How many 20 cents can I get from 1 dollar?", fakeService.ChatHistory[1].Content);
    }

    [Fact]
    public async Task ItParsesStandardizedPromptWhenServiceIsStreamingChatCompletionAsync()
    {
        var fakeService = new FakeChatAsTextService();
        KernelBuilder builder = new();
        builder.Services.AddTransient<ITextGenerationService>((sp) => fakeService);
        Kernel kernel = builder.Build();

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt(@"
            <message role=""system"">You are a helpful assistant.</message>
            <message role=""user"">How many 20 cents can I get from 1 dollar?</message>
        ");

        // Act + Assert
        await foreach (var chunk in kernel.InvokeStreamingAsync(function))
        {
        }

        Assert.NotNull(fakeService.ChatHistory);
        Assert.Equal(2, fakeService.ChatHistory.Count);
        Assert.Equal("You are a helpful assistant.", fakeService.ChatHistory[0].Content);
        Assert.Equal("How many 20 cents can I get from 1 dollar?", fakeService.ChatHistory[1].Content);
    }

    [Fact]
    public async Task ItNotParsesStandardizedPromptWhenServiceIsOnlyTextCompletionAsync()
    {
        var mockService = new Mock<ITextGenerationService>();
        var mockResult = mockService.Setup(s => s.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextContent>() { new("something") });

        KernelBuilder builder = new();
        builder.Services.AddTransient<ITextGenerationService>((sp) => mockService.Object);
        Kernel kernel = builder.Build();

        var inputPrompt = @"
            <message role=""system"">You are a helpful assistant.</message>
            <message role=""user"">How many 20 cents can I get from 1 dollar?</message>
        ";

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt(inputPrompt);

        // Act + Assert
        mockResult.Callback((string prompt, PromptExecutionSettings _, Kernel _, CancellationToken _) =>
        {
            Assert.NotNull(prompt);
            Assert.Equal(inputPrompt, prompt);
        });

        await kernel.InvokeAsync(function);
    }

    [Fact]
    public async Task ItNotParsesStandardizedPromptWhenStreamingWhenServiceIsOnlyTextCompletionAsync()
    {
        var mockService = new Mock<ITextGenerationService>();
        var mockResult = mockService.Setup(s => s.GetStreamingTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .Returns(this.ToAsyncEnumerable<StreamingTextContent>(new List<StreamingTextContent>() { new("something") }));

        KernelBuilder builder = new();
        builder.Services.AddTransient<ITextGenerationService>((sp) => mockService.Object);
        Kernel kernel = builder.Build();

        var inputPrompt = @"
            <message role=""system"">You are a helpful assistant.</message>
            <message role=""user"">How many 20 cents can I get from 1 dollar?</message>
        ";

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt(inputPrompt);

        // Act + Assert
        mockResult.Callback((string prompt, PromptExecutionSettings _, Kernel _, CancellationToken _) =>
        {
            Assert.NotNull(prompt);
            Assert.Equal(inputPrompt, prompt);
        });

        await foreach (var chunk in kernel.InvokeStreamingAsync(function))
        {
        }
    }

    [Fact]
    public async Task InvokeAsyncReturnsTheConnectorResultWhenInServiceIsOnlyTextCompletionAsync()
    {
        var mockService = new Mock<ITextGenerationService>();
        var mockResult = mockService.Setup(s => s.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextContent>() { new("something") });

        KernelBuilder builder = new();
        builder.Services.AddTransient<ITextGenerationService>((sp) => mockService.Object);
        Kernel kernel = builder.Build();

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt("Anything");

        var result = await kernel.InvokeAsync(function);

        Assert.Equal("something", result.GetValue<string>());
        Assert.Equal("something", result.GetValue<TextContent>()!.Text);
        Assert.Equal("something", result.GetValue<ContentBase>()!.ToString());
    }

    [Fact]
    public async Task InvokeAsyncReturnsTheConnectorChatResultWhenInServiceIsOnlyChatCompletionAsync()
    {
        var mockService = new Mock<IChatCompletionService>();
        var mockResult = mockService.Setup(s => s.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>() { new(AuthorRole.User, "something") });

        KernelBuilder builder = new();
        builder.Services.AddTransient<IChatCompletionService>((sp) => mockService.Object);
        Kernel kernel = builder.Build();

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt("Anything");

        var result = await kernel.InvokeAsync(function);

        Assert.Equal("something", result.GetValue<string>());
        Assert.Equal("something", result.GetValue<ChatMessageContent>()!.Content);
        Assert.Equal(AuthorRole.User, result.GetValue<ChatMessageContent>()!.Role);
        Assert.Equal("something", result.GetValue<ContentBase>()!.ToString());
    }

    [Fact]
    public async Task InvokeAsyncReturnsTheConnectorChatResultWhenInServiceIsChatAndTextCompletionAsync()
    {
        var fakeService = new FakeChatAsTextService();
        KernelBuilder builder = new();
        builder.Services.AddTransient<ITextGenerationService>((sp) => fakeService);
        Kernel kernel = builder.Build();

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt("Anything");

        var result = await kernel.InvokeAsync(function);

        Assert.Equal("Something", result.GetValue<string>());
        Assert.Equal("Something", result.GetValue<ChatMessageContent>()!.Content);
        Assert.Equal(AuthorRole.Assistant, result.GetValue<ChatMessageContent>()!.Role);
        Assert.Equal("Something", result.GetValue<ContentBase>()!.ToString());
    }

    [Fact]
    public async Task InvokeAsyncOfTGivesBackTheExpectedResultTypeFromTheConnectorWhenStreamingWhenServiceIsOnlyTextCompletionAsync()
    {
        var expectedContent = new StreamingTextContent("something");
        var mockService = new Mock<ITextGenerationService>();
        var mockResult = mockService.Setup(s => s.GetStreamingTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .Returns(this.ToAsyncEnumerable<StreamingTextContent>(new List<StreamingTextContent>() { expectedContent }));

        KernelBuilder builder = new();
        builder.Services.AddTransient<ITextGenerationService>((sp) => mockService.Object);
        Kernel kernel = builder.Build();

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt("Anything");

        await foreach (var chunk in kernel.InvokeStreamingAsync<StreamingContentBase>(function))
        {
            Assert.Equal(expectedContent, chunk);
        }

        await foreach (var chunk in kernel.InvokeStreamingAsync<StreamingTextContent>(function))
        {
            Assert.Equal(expectedContent, chunk);
        }
    }

    [Fact]
    public async Task InvokeAsyncOfTGivesBackTheExpectedResultTypeFromTheConnectorWhenStreamingWhenerviceIsOnlyChatCompletionAsync()
    {
        var expectedContent = new StreamingChatMessageContent(AuthorRole.Assistant, "Something");
        var mockService = new Mock<IChatCompletionService>();
        var mockResult = mockService.Setup(s => s.GetStreamingChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .Returns(this.ToAsyncEnumerable<StreamingChatMessageContent>(new List<StreamingChatMessageContent>() { expectedContent }));

        KernelBuilder builder = new();
        builder.Services.AddTransient<IChatCompletionService>((sp) => mockService.Object);
        Kernel kernel = builder.Build();

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt("Anything");

        await foreach (var chunk in kernel.InvokeStreamingAsync<StreamingContentBase>(function))
        {
            Assert.Equal(expectedContent, chunk);
            Assert.Equal("Something", chunk.ToString());
        }

        await foreach (var chunk in kernel.InvokeStreamingAsync<StreamingChatMessageContent>(function))
        {
            Assert.Equal(expectedContent, chunk);
            Assert.Equal("Something", chunk.Content);
            Assert.Equal(AuthorRole.Assistant, chunk.Role);
        }
    }

    [Fact]
    public async Task InvokeAsyncOfTGivesBackTheExpectedResultTypeFromTheConnectorWhenStreamingWhenServiceIsTextAndChatCompletionAsync()
    {
        var fakeService = new FakeChatAsTextService();
        KernelBuilder builder = new();
        builder.Services.AddTransient<ITextGenerationService>((sp) => fakeService);
        Kernel kernel = builder.Build();

        KernelFunction function = KernelFunctionFactory.CreateFromPrompt("Anything");

        await foreach (var chunk in kernel.InvokeStreamingAsync<StreamingContentBase>(function))
        {
            Assert.Equal("Something", chunk.ToString());
        }

        await foreach (var chunk in kernel.InvokeStreamingAsync<StreamingChatMessageContent>(function))
        {
            Assert.Equal(AuthorRole.Assistant, chunk.Role);
            Assert.Equal("Something", chunk.Content);
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable IDE1006 // Naming Styles
    private async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> enumeration)
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        foreach (var enumerationItem in enumeration)
        {
            yield return enumerationItem;
        }
    }

    private sealed class FakeChatAsTextService : ITextGenerationService, IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes => throw new NotImplementedException();
        public ChatHistory? ChatHistory { get; private set; }

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            this.ChatHistory = chatHistory;

            return Task.FromResult<IReadOnlyList<ChatMessageContent>>(new List<ChatMessageContent> { new(AuthorRole.Assistant, "Something") });
        }

#pragma warning disable IDE0036 // Order modifiers
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore IDE0036 // Order modifiers
        {
            this.ChatHistory = chatHistory;
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, "Something");
        }

        public IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<TextContent>> GetTextContentsAsync(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
