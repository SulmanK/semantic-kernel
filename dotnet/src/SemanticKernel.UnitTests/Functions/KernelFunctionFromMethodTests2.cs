﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Xunit;

namespace SemanticKernel.UnitTests.Functions;

public sealed class KernelFunctionFromMethodTests2
{
    private static readonly KernelFunction s_nopFunction = KernelFunctionFactory.CreateFromMethod(() => { });

    [Fact]
    public void ItDoesntThrowForValidFunctionsViaDelegate()
    {
        // Arrange
        var pluginInstance = new LocalExamplePlugin();
        MethodInfo[] methods = pluginInstance.GetType()
            .GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod)
            .Where(m => m.Name is not "GetType" and not "Equals" and not "GetHashCode" and not "ToString")
            .ToArray();

        KernelFunction[] functions = (from method in methods select KernelFunctionFactory.CreateFromMethod(method, pluginInstance, "plugin")).ToArray();

        // Act
        Assert.Equal(methods.Length, functions.Length);
        Assert.All(functions, Assert.NotNull);
    }

    [Fact]
    public void ItDoesNotThrowForValidFunctionsViaPlugin()
    {
        // Arrange
        var pluginInstance = new LocalExamplePlugin();
        MethodInfo[] methods = pluginInstance.GetType()
            .GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod)
            .Where(m => m.Name is not "GetType" and not "Equals" and not "GetHashCode" and not "ToString")
            .ToArray();

        KernelFunction[] functions = KernelPluginFactory.CreateFromObject(pluginInstance).ToArray();

        // Act
        Assert.Equal(methods.Length, functions.Length);
        Assert.All(functions, f => Assert.NotNull(f));
    }

    [Fact]
    public async Task ItCanImportMethodFunctionsAsync()
    {
        // Arrange
        var canary = false;

        // Note: the function doesn't have any SK attributes
        async Task ExecuteAsync(string done)
        {
            Assert.Equal("NO", done);
            canary = true;
            await Task.Delay(0);
        }

        // Act
        KernelFunction function = KernelFunctionFactory.CreateFromMethod(
            method: ExecuteAsync,
            parameters: null,
            description: "description",
            functionName: "functionName");

        FunctionResult result = await function.InvokeAsync(new(), new KernelArguments
        {
            ["done"] = "NO"
        });

        // Assert
        Assert.True(canary);
        Assert.Null(result.GetValue<object?>());
        Assert.Empty(result.ToString());
    }

    [Fact]
    public async Task ItCanImportMethodFunctionsWithExternalReferencesAsync()
    {
        // Arrange
        var arguments = new KernelArguments();
        arguments["done"] = "NO";

        // Note: This is an important edge case that affects the function signature and how delegates
        //       are handled internally: the function references an external variable and cannot be static.
        //       This scenario is used for gRPC functions.
        string variableOutsideTheFunction = "foo";

        async Task<string> ExecuteAsync(string done)
        {
            string referenceToExternalVariable = variableOutsideTheFunction;
            await Task.Delay(0);
            return referenceToExternalVariable;
        }

        // Act. Note: this will throw an exception if the KernelFunction doesn't handle the function type.
        KernelFunction function = KernelFunctionFactory.CreateFromMethod(
            method: ExecuteAsync,
            description: "description",
            functionName: "functionName");

        FunctionResult result = await function.InvokeAsync(new(), arguments);

        // Assert
        Assert.Equal(variableOutsideTheFunction, result.GetValue<string>());
        Assert.Equal(variableOutsideTheFunction, result.ToString());
    }

    [Fact]
    public async Task ItFlowsSpecialArgumentsIntoFunctionsAsync()
    {
        KernelBuilder builder = new();
        builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Warning));
        Kernel kernel = builder.Build();
        kernel.Culture = new CultureInfo("fr-FR");
        KernelArguments args = new();
        using CancellationTokenSource cts = new();

        bool invoked = false;
        KernelFunction func = null!;
        func = KernelFunctionFactory.CreateFromMethod(
            (Kernel kernelArg, KernelFunction funcArg, KernelArguments argsArg, ILoggerFactory loggerFactoryArg,
             ILogger loggerArg, IAIServiceSelector serviceSelectorArg, CultureInfo cultureArg, CancellationToken cancellationToken) =>
            {
                Assert.Same(kernel, kernelArg);
                Assert.Same(func, funcArg);
                Assert.Same(args, argsArg);
                Assert.Same(kernel.LoggerFactory, loggerFactoryArg);
                Assert.NotNull(loggerArg);
                Assert.Same(kernel.ServiceSelector, serviceSelectorArg);
                Assert.Same(kernel.Culture, cultureArg);
                Assert.Equal(cts.Token, cancellationToken);
                invoked = true;
            });

        await func.InvokeAsync(kernel, args, cts.Token);

        Assert.True(invoked);
    }

    [Fact]
    public async Task ItInjectsServicesFromDIIntoFunctionsAsync()
    {
        var serviceA = new ExampleService();
        var serviceB = new ExampleService();
        var serviceC = new ExampleService();

        KernelBuilder builder = new();
        builder.Services.AddKeyedSingleton<IExampleService>("something", serviceA);
        builder.Services.AddSingleton<IExampleService>(serviceB);
        builder.Services.AddKeyedSingleton<IExampleService>("somethingelse", serviceC);
        Kernel kernel = builder.Build();

        bool invoked = false;
        KernelFunction func = KernelFunctionFactory.CreateFromMethod(
            ([FromKernelServices] IExampleService service1Arg,
             [FromKernelServices("something")] IExampleService service2Arg,
             [FromKernelServices("somethingelse")] IExampleService service3Arg,
             [FromKernelServices] IExampleService service4Arg,
             [FromKernelServices("doesntexist")] IExampleService? service5Arg = null) =>
            {
                Assert.Same(serviceB, service1Arg);
                Assert.Same(serviceA, service2Arg);
                Assert.Same(serviceC, service3Arg);
                Assert.Same(serviceB, service4Arg);
                Assert.Null(service5Arg);
                invoked = true;
            });

        await func.InvokeAsync(kernel);

        Assert.True(invoked);

        Assert.DoesNotContain(func.Metadata.Parameters, p => p.Name.Contains("service", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ItThrowsForMissingServicesWithoutDefaultsAsync()
    {
        Kernel kernel = new();
        KernelFunction func;

        func = KernelFunctionFactory.CreateFromMethod(([FromKernelServices] IExampleService service) => { });
        await Assert.ThrowsAsync<KernelException>(() => func.InvokeAsync(kernel));

        func = KernelFunctionFactory.CreateFromMethod(([FromKernelServices] IExampleService? service) => { });
        await Assert.ThrowsAsync<KernelException>(() => func.InvokeAsync(kernel));

        func = KernelFunctionFactory.CreateFromMethod(([FromKernelServices("name")] IExampleService? service) => { });
        await Assert.ThrowsAsync<KernelException>(() => func.InvokeAsync(kernel));
    }

    private interface IExampleService
    {
    }

    private sealed class ExampleService : IExampleService
    {
    }

    private sealed class LocalExamplePlugin
    {
        [KernelFunction]
        public void Type01()
        {
        }

        [KernelFunction]
        public string Type02()
        {
            return "";
        }

        [KernelFunction]
        public string? Type02Nullable()
        {
            return null;
        }

        [KernelFunction]
        public async Task<string> Type03Async()
        {
            await Task.Delay(0);
            return "";
        }

        [KernelFunction]
        public async Task<string?> Type03NullableAsync()
        {
            await Task.Delay(0);
            return null;
        }

        [KernelFunction]
        public void Type04(string input)
        {
        }

        [KernelFunction]
        public void Type04Nullable(string? input)
        {
        }

        [KernelFunction]
        public string Type05(string input)
        {
            return "";
        }

        [KernelFunction]
        public string? Type05Nullable(string? input = null)
        {
            return "";
        }

        [KernelFunction]
        public async Task<string> Type06Async(string input)
        {
            await Task.Delay(0);
            return "";
        }

        [KernelFunction]
        public async Task<string?> Type06NullableAsync(string? input)
        {
            await Task.Delay(0);
            return "";
        }

        [KernelFunction]
        public async Task Type07Async(string input)
        {
            await Task.Delay(0);
        }

        [KernelFunction]
        public async Task Type08Async()
        {
            await Task.Delay(0);
        }

        [KernelFunction]
        public async ValueTask ReturnsValueTaskAsync()
        {
            await Task.Delay(0);
        }

        [KernelFunction]
        public async ValueTask<string> ReturnsValueTaskStringAsync()
        {
            await Task.Delay(0);
            return "hello world";
        }

        [KernelFunction]
        public FunctionResult ReturnsFunctionResult()
        {
            return new FunctionResult(s_nopFunction, "fake-result", CultureInfo.InvariantCulture);
        }

        [KernelFunction]
        public async Task<FunctionResult> ReturnsTaskFunctionResultAsync()
        {
            await Task.Delay(0);
            return new FunctionResult(s_nopFunction, "fake-result", CultureInfo.InvariantCulture);
        }

        [KernelFunction]
        public async ValueTask<FunctionResult> ReturnsValueTaskFunctionResultAsync()
        {
            await Task.Delay(0);
            return new FunctionResult(s_nopFunction, "fake-result", CultureInfo.InvariantCulture);
        }

        [KernelFunction]
        public string WithPrimitives(
            byte a1,
            byte? b1,
            sbyte c1,
            sbyte? d1,
            short e1,
            short? f1,
            ushort g1,
            ushort? h1,
            int i1,
            int? j1,
            uint k1,
            uint? l1,
            long m1,
            long? n1,
            ulong o1,
            ulong? p1,
            float q1,
            float? r1,
            double s1,
            double? t1,
            decimal u1,
            decimal? v1,
            char w1,
            char? x1,
            bool y1,
            bool? z1,
            DateTime a2,
            DateTime? b2,
            DateTimeOffset c2,
            DateTimeOffset? d2,
            TimeSpan e2,
            TimeSpan? f2,
            Guid g2,
            Guid? h2,
            DayOfWeek i2,
            DayOfWeek? j2,
            Uri k2,
            string l2)
        {
            return string.Empty;
        }
    }
}
