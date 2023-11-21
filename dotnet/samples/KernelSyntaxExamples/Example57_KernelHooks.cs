﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Orchestration;
using RepoUtils;

// ReSharper disable once InconsistentNaming
public static class Example57_KernelHooks
{
    private static string? s_openAIModelId;
    private static string? s_openAIApiKey;

    public static async Task RunAsync()
    {
        Console.WriteLine("\n======== Using Function Execution Handlers ========\n");

        s_openAIModelId = TestConfiguration.OpenAI.ChatModelId;
        s_openAIApiKey = TestConfiguration.OpenAI.ApiKey;

        if (s_openAIModelId == null || s_openAIApiKey == null)
        {
            Console.WriteLine("OpenAI credentials not found. Skipping example.");
            return;
        }

        await GetUsageAsync();

        await GetRenderedPromptAsync();

        await ChangingResultAsync();

        await BeforeInvokeCancellationAsync();

        await AfterInvokeCancellationAsync();

        await SkippingFunctionsAsync();

        await RepeatFunctionsAsync();
    }

    private static async Task GetUsageAsync()
    {
        Console.WriteLine("\n======== Get Usage Data ========\n");

        Kernel kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAIChatCompletionService(
                modelId: s_openAIModelId!,
                apiKey: s_openAIApiKey!)
            .Build();

        const string FunctionPrompt = "Write a random paragraph about: {{$input}}.";

        var excuseFunction = kernel.CreateFunctionFromPrompt(
            FunctionPrompt,
            functionName: "Excuse",
            requestSettings: new OpenAIRequestSettings() { MaxTokens = 100, Temperature = 0.4, TopP = 1 });

        void MyPreHandler(object? sender, FunctionInvokingEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.PluginName}.{e.FunctionView.Name} : Pre Execution Handler - Triggered");
        }

        void MyRemovedPreExecutionHandler(object? sender, FunctionInvokingEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.PluginName}.{e.FunctionView.Name} : Pre Execution Handler - Should not trigger");
            e.Cancel();
        }

        void MyPostExecutionHandler(object? sender, FunctionInvokedEventArgs e)
        {
            var modelResults = e.Metadata["ModelResults"] as IReadOnlyCollection<ModelResult>;
            Console.WriteLine($"{e.FunctionView.PluginName}.{e.FunctionView.Name} : Post Execution Handler - Total Tokens: {modelResults?.First().GetOpenAIChatResult().Usage.TotalTokens}");
        }

        kernel.FunctionInvoking += MyPreHandler;
        kernel.FunctionInvoked += MyPostExecutionHandler;

        // Adding and Removing a handler
        kernel.FunctionInvoking += MyRemovedPreExecutionHandler;
        kernel.FunctionInvoking -= MyRemovedPreExecutionHandler;

        const string Input = "I missed the F1 final race";
        var result = await kernel.RunAsync(Input, excuseFunction);
        Console.WriteLine($"Function Result: {result.GetValue<string>()}");
    }

    private static async Task GetRenderedPromptAsync()
    {
        Console.WriteLine("\n======== Get Rendered Prompt ========\n");

        Kernel kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAIChatCompletionService(
                modelId: s_openAIModelId!,
                apiKey: s_openAIApiKey!)
            .Build();

        const string FunctionPrompt = "Write a random paragraph about: {{$input}}.";

        var excuseFunction = kernel.CreateFunctionFromPrompt(
            FunctionPrompt,
            functionName: "Excuse",
            requestSettings: new OpenAIRequestSettings() { MaxTokens = 100, Temperature = 0.4, TopP = 1 });

        void MyPreHandler(object? sender, FunctionInvokingEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.PluginName}.{e.FunctionView.Name} : Pre Execution Handler - Triggered");

            if (e.TryGetRenderedPrompt(out var prompt))
            {
                // Get the rendered prompt when available
                Console.WriteLine("Rendered Prompt:");
                Console.WriteLine(prompt);

                // Update the prompt
                e.TryUpdateRenderedPrompt($"{prompt}. USE SHORT, CLEAR, COMPLETE SENTENCES.");
            }
        }

        void MyPostHandler(object? sender, FunctionInvokedEventArgs e)
        {
            Console.WriteLine($"{e.FunctionView.PluginName}.{e.FunctionView.Name} : Post Execution Handler - Triggered");
            // Will be false for non semantic functions
            if (e.TryGetRenderedPrompt(out var prompt))
            {
                Console.WriteLine("Used Prompt:");
                Console.WriteLine(prompt);
            }
        }

        kernel.FunctionInvoking += MyPreHandler;
        kernel.FunctionInvoked += MyPostHandler;

        const string Input = "I missed the F1 final race";
        var result = await kernel.RunAsync(Input, excuseFunction);
        Console.WriteLine($"Function Result: {result.GetValue<string>()}");
    }

    private static async Task ChangingResultAsync()
    {
        Console.WriteLine("\n======== Changing/Filtering Function Result ========\n");

        Kernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: s_openAIModelId!,
               apiKey: s_openAIApiKey!)
           .Build();

        const string FunctionPrompt = "Write a paragraph about Handlers.";

        var writerFunction = kernel.CreateFunctionFromPrompt(
            FunctionPrompt,
            functionName: "Writer",
            requestSettings: new OpenAIRequestSettings() { MaxTokens = 100, Temperature = 0.4, TopP = 1 });

        void MyChangeDataHandler(object? sender, FunctionInvokedEventArgs e)
        {
            var originalOutput = e.SKContext.Variables.Input;

            //Use Regex to redact all vowels and numbers
            var newOutput = Regex.Replace(originalOutput, "[aeiouAEIOU0-9]", "*");

            e.SKContext.Variables.Update(newOutput);
        }

        kernel.FunctionInvoked += MyChangeDataHandler;

        var result = await kernel.RunAsync(writerFunction);

        Console.WriteLine($"Function Result: {result.GetValue<string>()}");
    }

    private static async Task BeforeInvokeCancellationAsync()
    {
        Console.WriteLine("\n======== Cancelling Pipeline Execution - Invoking event ========\n");

        Kernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: s_openAIModelId!,
               apiKey: s_openAIApiKey!)
           .Build();

        const string FunctionPrompt = "Write a paragraph about: Cancellation.";

        var writerFunction = kernel.CreateFunctionFromPrompt(
            FunctionPrompt,
            functionName: "Writer",
            requestSettings: new OpenAIRequestSettings() { MaxTokens = 1000, Temperature = 1, TopP = 0.5 });

        // Adding new inline handler to cancel/prevent function execution
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            Console.WriteLine($"{e.FunctionView.PluginName}.{e.FunctionView.Name} : FunctionInvoking - Cancelling all subsequent invocations");
            e.Cancel();
        };

        // Technically invoked will never be called since the function will be cancelled
        int functionInvokedCount = 0;
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            functionInvokedCount++;
        };

        var result = await kernel.RunAsync(writerFunction);
        Console.WriteLine($"Function Invocation Times: {functionInvokedCount}");
    }

    private static async Task AfterInvokeCancellationAsync()
    {
        Console.WriteLine("\n======== Cancelling Pipeline Execution - Invoked event ========\n");

        Kernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: s_openAIModelId!,
               apiKey: s_openAIApiKey!)
           .Build();

        int functionInvokingCount = 0;
        int functionInvokedCount = 0;

        var firstFunction = kernel.CreateFunctionFromPrompt("Write a phrase with Invoke.", functionName: "InvokePhrase");
        var secondFunction = kernel.CreateFunctionFromPrompt("Write a phrase with Cancellation.", functionName: "CancellationPhrase");

        // Adding new inline handler to count invoking events
        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            functionInvokingCount++;
        };

        // Invoked will never be called twice (for the secondFunction) since Invoked from the first is cancelling.
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            functionInvokedCount++;
            e.Cancel();
        };

        var result = await kernel.RunAsync(secondFunction);
        Console.WriteLine($"Function Invoked Times: {functionInvokedCount}");
        Console.WriteLine($"Function Invoking Times: {functionInvokingCount}");
    }

    private static async Task SkippingFunctionsAsync()
    {
        Console.WriteLine("\n======== Skipping a Function in the Pipeline ========\n");

        Kernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: s_openAIModelId!,
               apiKey: s_openAIApiKey!)
           .Build();

        var skipMeFunction = kernel.CreateFunctionFromPrompt("Write a paragraph about Skipping",
            functionName: "SkipMe");

        var dontSkipMeFunction = kernel.CreateFunctionFromPrompt("Write a paragraph about Handlers",
            functionName: "DontSkipMe");

        kernel.FunctionInvoking += (object? sender, FunctionInvokingEventArgs e) =>
        {
            if (e.FunctionView.Name == "SkipMe")
            {
                e.Skip();
                Console.WriteLine($"Function {e.FunctionView.Name} will be skipped");
                return;
            }

            Console.WriteLine($"Function {e.FunctionView.Name} will not be skipped");
        };

        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            Console.WriteLine($"Only not skipped functions will trigger invoked event - Function name: {e.FunctionView.Name}");
        };

        var result = await kernel.RunAsync(
            skipMeFunction,
            dontSkipMeFunction);

        Console.WriteLine($"Final result: {result.GetValue<string>()}");
    }

    private static async Task RepeatFunctionsAsync()
    {
        Console.WriteLine("\n======== Repeating a Function in the Pipeline ========");

        Kernel kernel = new KernelBuilder()
           .WithLoggerFactory(ConsoleLogger.LoggerFactory)
           .WithOpenAIChatCompletionService(
               modelId: s_openAIModelId!,
               apiKey: s_openAIApiKey!)
           .Build();

        var repeatSubjects = new Queue<string>(new[] { "Life", "Work", "Leisure" });

        var repeatMeFunction = kernel.CreateFunctionFromPrompt("Write a sentence about {{$input}}",
            functionName: "RepeatMe");

        var repeatTimes = 0;
        kernel.FunctionInvoked += (object? sender, FunctionInvokedEventArgs e) =>
        {
            Console.WriteLine($"\nFunction {e.FunctionView.Name} executed:");
            Console.WriteLine($"Result: {e.SKContext.Variables.Input}");

            if (repeatTimes < 3)
            {
                // Flag the Kernel to repeat the function
                e.Repeat();

                // Redefine the input variable to repeat the function
                e.SKContext.Variables.Update(repeatSubjects.Dequeue());

                repeatTimes++;
                Console.WriteLine("Repeat requested!");

                return;
            }
        };

        await kernel.RunAsync("Repetition", repeatMeFunction);
    }
}
