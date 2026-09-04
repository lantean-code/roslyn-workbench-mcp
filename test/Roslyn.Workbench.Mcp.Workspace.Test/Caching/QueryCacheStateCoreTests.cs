using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Hosting;

using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.Diagnostics;
using Roslyn.Workbench.Mcp.Workspace.Test.Diagnostics;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Caching;

public sealed class QueryCacheStateCoreTests
{
    [Fact]
    public void GIVEN_CompletedNonAdmittedComputation_WHEN_Collecting_THEN_ShouldReleaseLinkedCancellationRegistrations()
    {
        using var stoppingSource = new CancellationTokenSource();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(stoppingSource.Token);

        using var target = new QueryCacheStateCore(
            1,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "LinkedSourceTest");

        var keyReference = CreateCompletedNonAdmittedComputation(target);

        CollectGarbage();

        keyReference.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_FullCache_WHEN_StoringAnotherEntry_THEN_ShouldRecordAdmissionRefusalWithoutFalseAdmission()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        var cacheFamily = $"CapacityTest-{Guid.NewGuid():N}";
        using var target = new QueryCacheStateCore(
            1,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            cacheFamily);

        var eventSource = WorkbenchPerformanceEventSource.Log;
        using var listener = new WorkbenchPerformanceEventListener(eventSource);
        var scope = target.CreateScope("Partition", "Scope");
        var firstKey = new TestKey("First");
        var secondKey = new TestKey("Second");

        target.GetOrCreate(
            scope,
            firstKey,
            static _ => new TestValue("First"),
            static _ => 1,
            static _ => true,
            CancellationToken.None);

        target.GetOrCreate(
            scope,
            secondKey,
            static _ => new TestValue("Second"),
            static _ => 1,
            static _ => true,
            CancellationToken.None);

        var matchingEvents = listener.Events
            .Where(item => IsCacheMetric(item, cacheFamily))
            .ToArray();

        matchingEvents.Count(item => GetMetric(item) == "admissions").Should().Be(1);
        matchingEvents
            .Where(item => GetMetric(item) == "capacity-admission-refusals")
            .Sum(GetMetricValue)
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task GIVEN_NonAdmittedSharedValue_WHEN_RequestedConcurrently_THEN_ShouldExecuteFactoryOnce()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            1,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "SharedNonAdmissionTest");

        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");
        var factoryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCompletion = new TaskCompletionSource<TestValue>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        var first = target.GetOrCreateAsync(
            scope,
            key,
            async _ =>
            {
                factoryCalls++;
                factoryStarted.SetResult();
                return await factoryCompletion.Task;
            },
            static _ => 1,
            static _ => false,
            CancellationToken.None).AsTask();

        await factoryStarted.Task;

        var unexpected = new TestValue("Unexpected");
        var second = target.GetOrCreateAsync(
            scope,
            key,
            _ => ValueTask.FromResult<TestValue?>(unexpected),
            static _ => 1,
            static _ => false,
            CancellationToken.None).AsTask();

        var expected = new TestValue("Value");
        factoryCompletion.SetResult(expected);

        var results = await Task.WhenAll(first, second);

        results.Should().AllSatisfy(item => item.Should().BeSameAs(expected));
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_ProjectedComputation_WHEN_ReusingValue_THEN_ShouldCacheOnlyProjectedData()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "ProjectedValueTest");

        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");
        var factoryResult = new TestComputationResult("Value", shouldCache: true);
        var unexpectedResult = new TestComputationResult("Unexpected", shouldCache: true);
        var factoryCalls = 0;

        var first = target.GetOrCreateProjected<TestKey, TestValue, TestComputationResult>(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return factoryResult;
            },
            static result => SelectTestCacheValue(result),
            static value => CreateTestComputationResult(value),
            static _ => 1,
            CancellationToken.None);

        var second = target.GetOrCreateProjected<TestKey, TestValue, TestComputationResult>(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return unexpectedResult;
            },
            static result => SelectTestCacheValue(result),
            static value => CreateTestComputationResult(value),
            static _ => 1,
            CancellationToken.None);

        var found = target.TryGet<TestKey, TestValue>(
            scope,
            key,
            out var cachedValue);

        first.Should().BeSameAs(factoryResult);
        second.Should().NotBeSameAs(factoryResult);
        second.Value.Should().Be("Value");
        factoryCalls.Should().Be(1);
        found.Should().BeTrue();
        cachedValue.Should().BeOfType<TestValue>();
    }

    [Fact]
    public async Task GIVEN_NonAdmittedProjectedResult_WHEN_RequestedConcurrently_THEN_ShouldShareWithoutCaching()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        var cacheFamily = $"ProjectedNonAdmissionTest-{Guid.NewGuid():N}";
        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            cacheFamily);

        var eventSource = WorkbenchPerformanceEventSource.Log;
        using var listener = new WorkbenchPerformanceEventListener(eventSource);
        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");
        var factoryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCompletion = new TaskCompletionSource<TestComputationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        var first = Task.Run(() => target.GetOrCreateProjected<TestKey, TestValue, TestComputationResult>(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                factoryStarted.SetResult();
                return factoryCompletion.Task.GetAwaiter().GetResult();
            },
            static result => SelectTestCacheValue(result),
            static value => CreateTestComputationResult(value),
            static _ => 1,
            CancellationToken.None));

        await factoryStarted.Task;

        var unexpectedResult = new TestComputationResult("Unexpected", shouldCache: false);
        var second = Task.Run(() => target.GetOrCreateProjected<TestKey, TestValue, TestComputationResult>(
            scope,
            key,
            _ =>
            {
                factoryCalls++;
                return unexpectedResult;
            },
            static result => SelectTestCacheValue(result),
            static value => CreateTestComputationResult(value),
            static _ => 1,
            CancellationToken.None));

        await WaitForMetricAsync(
            listener,
            cacheFamily,
            "in-flight-joins",
            TestContext.Current.CancellationToken);

        var sharedResult = new TestComputationResult("Failure", shouldCache: false);
        factoryCompletion.SetResult(sharedResult);

        var results = await Task.WhenAll(first, second);

        var found = target.TryGet<TestKey, TestValue>(
            scope,
            key,
            out var cachedValue);

        results.Should().AllSatisfy(item => item.Should().BeSameAs(sharedResult));
        factoryCalls.Should().Be(1);
        found.Should().BeFalse();
        cachedValue.Should().BeNull();
    }

    [Fact]
    public void GIVEN_HostBatchEntry_WHEN_StoringAndProbing_THEN_ShouldReturnStoredValueAndReportOtherKeysMissing()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "BatchProbeTest");

        var scope = target.CreateScope("Partition", "Scope");
        var storedKey = new TestKey("Stored");
        var missingKey = new TestKey("Missing");
        var expected = new TestValue("Value");
        target.Store(
            scope,
            storedKey,
            expected,
            static _ => 1);

        var found = target.TryGet<TestKey, TestValue>(
            scope,
            storedKey,
            out var stored);

        var missing = target.TryGet<TestKey, TestValue>(
            scope,
            missingKey,
            out var absent);

        found.Should().BeTrue();
        stored.Should().BeSameAs(expected);
        missing.Should().BeFalse();
        absent.Should().BeNull();
    }

    [Fact]
    public void GIVEN_InvalidatedGeneration_WHEN_StoringBatchEntry_THEN_ShouldRejectLateStore()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "LateBatchStoreTest");

        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");
        var value = new TestValue("Value");
        target.InvalidatePartition("Partition");

        target.Store(
            scope,
            key,
            value,
            static _ => 1);

        var found = target.TryGet<TestKey, TestValue>(
            scope,
            key,
            out var cachedValue);

        found.Should().BeFalse();
        cachedValue.Should().BeNull();
    }

    [Fact]
    public void GIVEN_MissingPartition_WHEN_Invalidating_THEN_ShouldRemainUsable()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "MissingPartitionTest");

        target.InvalidatePartition("MissingPartition");

        var scope = target.CreateScope("Partition", "Scope");
        scope.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void GIVEN_InvalidEntryCharge_WHEN_GettingTwice_THEN_ShouldNotCacheValue(long entryCharge)
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "InvalidChargeTest");

        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");
        var factoryCalls = 0;

        TestValue? CreateValue(CancellationToken _)
        {
            factoryCalls++;
            return new TestValue($"Value{factoryCalls}");
        }

        var first = target.GetOrCreate(
            scope,
            key,
            CreateValue,
            _ => entryCharge,
            static _ => true,
            CancellationToken.None);

        var second = target.GetOrCreate(
            scope,
            key,
            CreateValue,
            _ => entryCharge,
            static _ => true,
            CancellationToken.None);

        first.Should().NotBeSameAs(second);
        factoryCalls.Should().Be(2);
    }

    [Fact]
    public void GIVEN_RecursiveFactory_WHEN_GettingSameEntry_THEN_ShouldRejectRecursion()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "RecursiveFactoryTest");

        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");

        var action = () => target.GetOrCreate<TestKey, TestValue>(
            scope,
            key,
            _ => target.GetOrCreate(
                scope,
                key,
                static _ => new TestValue("NestedValue"),
                static _ => 1,
                static _ => true,
                CancellationToken.None),
            static _ => 1,
            static _ => true,
            CancellationToken.None);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("A query-cache value factory cannot recursively request its own scoped key.");
    }

    [Fact]
    public async Task GIVEN_InvalidatedInFlightFactory_WHEN_Waiting_THEN_ShouldCancelFactory()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "InvalidatedFactoryTest");

        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");
        var factoryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var neverCompletes = new TaskCompletionSource<TestValue>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var result = target.GetOrCreateAsync(
            scope,
            key,
            async token =>
            {
                factoryStarted.SetResult();
                return await neverCompletes.Task.WaitAsync(token);
            },
            static _ => 1,
            static _ => true,
            CancellationToken.None).AsTask();

        await factoryStarted.Task;
        target.InvalidatePartition("Partition");

        var action = async () => await result;

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_CancelledOnlyWaiter_WHEN_FactoryIsRunning_THEN_ShouldCancelFactoryComputation()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "CancelledWaiterTest");

        using var callerCancellation = new CancellationTokenSource();
        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");
        var factoryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var neverCompletes = new TaskCompletionSource<TestValue>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryToken = CancellationToken.None;

        var result = target.GetOrCreateAsync(
            scope,
            key,
            async token =>
            {
                factoryToken = token;
                factoryStarted.SetResult();
                return await neverCompletes.Task.WaitAsync(token);
            },
            static _ => 1,
            static _ => true,
            callerCancellation.Token).AsTask();

        await factoryStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        CancelSynchronously(callerCancellation);

        var action = async () => await result.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<OperationCanceledException>();
        factoryToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_TwoWaiters_WHEN_OneWaiterIsCancelled_THEN_ShouldKeepSharedFactoryRunning()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        var cacheFamily = $"RemainingWaiterTest-{Guid.NewGuid():N}";
        using var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            cacheFamily);

        using var firstCallerCancellation = new CancellationTokenSource();
        var eventSource = WorkbenchPerformanceEventSource.Log;
        using var listener = new WorkbenchPerformanceEventListener(eventSource);
        var scope = target.CreateScope("Partition", "Scope");
        var key = new TestKey("Key");
        var factoryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCompletion = new TaskCompletionSource<TestValue>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;
        var factoryToken = CancellationToken.None;

        var first = target.GetOrCreateAsync(
            scope,
            key,
            async token =>
            {
                factoryCalls++;
                factoryToken = token;
                factoryStarted.SetResult();
                return await factoryCompletion.Task.WaitAsync(token);
            },
            static _ => 1,
            static _ => true,
            firstCallerCancellation.Token).AsTask();

        await factoryStarted.Task;

        var second = target.GetOrCreateAsync(
            scope,
            key,
            _ => ValueTask.FromResult<TestValue?>(new TestValue("Unexpected")),
            static _ => 1,
            static _ => true,
            CancellationToken.None).AsTask();

        await WaitForMetricAsync(
            listener,
            cacheFamily,
            "in-flight-joins",
            TestContext.Current.CancellationToken);

        CancelSynchronously(firstCallerCancellation);

        var firstAction = async () => await first.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        await firstAction.Should().ThrowAsync<OperationCanceledException>();
        factoryToken.IsCancellationRequested.Should().BeFalse();

        var expected = new TestValue("Value");
        factoryCompletion.SetResult(expected);

        var secondResult = await second;

        secondResult.Should().BeSameAs(expected);
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public void GIVEN_DisposedCore_WHEN_DisposingAgain_THEN_ShouldNotThrow()
    {
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime
            .SetupGet(item => item.ApplicationStopping)
            .Returns(CancellationToken.None);

        var target = new QueryCacheStateCore(
            10,
            TimeSpan.FromHours(1),
            applicationLifetime.Object,
            "RepeatedDisposeTest");

        target.Dispose();

        var action = target.Dispose;

        action.Should().NotThrow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCompletedNonAdmittedComputation(QueryCacheStateCore target)
    {
        var key = new TestKey("Key");
        var keyReference = new WeakReference(key);
        var scope = target.CreateScope("Partition", "Scope");

        target.GetOrCreate(
            scope,
            key,
            static _ => new TestValue("Value"),
            static _ => 1,
            static _ => false,
            CancellationToken.None);

        return keyReference;
    }

    private static void CollectGarbage()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static void CancelSynchronously(CancellationTokenSource source)
    {
        source.Cancel();
    }

    private static bool IsCacheMetric(EventWrittenEventArgs traceEvent, string cacheFamily)
    {
        return traceEvent.EventName == "CacheMetric"
            && traceEvent.Payload is { Count: 3 }
            && Equals(traceEvent.Payload[0], cacheFamily);
    }

    private static string? GetMetric(EventWrittenEventArgs traceEvent)
    {
        return traceEvent.Payload?[1] as string;
    }

    private static long GetMetricValue(EventWrittenEventArgs traceEvent)
    {
        return traceEvent.Payload?[2] as long? ?? 0;
    }

    private static async Task WaitForMetricAsync(
        WorkbenchPerformanceEventListener listener,
        string cacheFamily,
        string metric,
        CancellationToken cancellationToken)
    {
        await listener.WaitForEventAsync(
            item =>
            IsCacheMetric(item, cacheFamily)
                && GetMetric(item) == metric,
            cancellationToken);
    }

    private static TestValue? SelectTestCacheValue(TestComputationResult result)
    {
        if (!result.ShouldCache)
        {
            return null;
        }

        var value = new TestValue(result.Value);
        return value;
    }

    private static TestComputationResult CreateTestComputationResult(TestValue value)
    {
        var result = new TestComputationResult(value.Value, shouldCache: true);
        return result;
    }

    private sealed record TestKey
    {
        public string Value { get; }

        public TestKey(string value)
        {
            Value = value;
        }
    }

    private sealed record TestValue
    {
        public string Value { get; }

        public TestValue(string value)
        {
            Value = value;
        }
    }

    private sealed record TestComputationResult
    {
        public string Value { get; }

        public bool ShouldCache { get; }

        public TestComputationResult(string value, bool shouldCache)
        {
            Value = value;
            ShouldCache = shouldCache;
        }
    }
}
