﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using System.Runtime.ExceptionServices;

namespace BlazorSchool.Components.Web.Tests.TestCore;
public class TestRenderer : Renderer
{
    public TestRenderer() : this(new TestServiceProvider())
    {
    }

    public TestRenderer(Dispatcher dispatcher) : base(new TestServiceProvider(), NullLoggerFactory.Instance)
    {
        Dispatcher = dispatcher;
    }

    public TestRenderer(IServiceProvider serviceProvider) : base(serviceProvider, NullLoggerFactory.Instance)
    {
        Dispatcher = Dispatcher.CreateDefault();
    }

    public TestRenderer(IServiceProvider serviceProvider, IComponentActivator componentActivator) : base(serviceProvider, NullLoggerFactory.Instance, componentActivator)
    {
        Dispatcher = Dispatcher.CreateDefault();
    }

    public override Dispatcher Dispatcher { get; }

    public Action OnExceptionHandled { get; set; }

    public Action<RenderBatch> OnUpdateDisplay { get; set; }

    public Action OnUpdateDisplayComplete { get; set; }

    public List<CapturedBatch> Batches { get; }
        = new List<CapturedBatch>();

    public List<Exception> HandledExceptions { get; } = new List<Exception>();

    public bool ShouldHandleExceptions { get; set; }

    public Task NextRenderResultTask { get; set; } = Task.CompletedTask;

    public new int AssignRootComponentId(IComponent component) => base.AssignRootComponentId(component);

    public new void RemoveRootComponent(int componentId) => base.RemoveRootComponent(componentId);

    public new ArrayRange<RenderTreeFrame> GetCurrentRenderTreeFrames(int componentId) => base.GetCurrentRenderTreeFrames(componentId);

    public void RenderRootComponent(int componentId, ParameterView? parameters = default)
    {
        var task = Dispatcher.InvokeAsync(() => base.RenderRootComponentAsync(componentId, parameters ?? ParameterView.Empty));
        _ = UnwrapTask(task);
    }

    public new Task RenderRootComponentAsync(int componentId) => Dispatcher.InvokeAsync(() => base.RenderRootComponentAsync(componentId));

    public new Task RenderRootComponentAsync(int componentId, ParameterView parameters) => Dispatcher.InvokeAsync(() => base.RenderRootComponentAsync(componentId, parameters));

    public Task DispatchEventAsync(ulong eventHandlerId, EventArgs args) => Dispatcher.InvokeAsync(() => base.DispatchEventAsync(eventHandlerId, null, args));

    public new Task DispatchEventAsync(ulong eventHandlerId, EventFieldInfo eventFieldInfo, EventArgs args) => Dispatcher.InvokeAsync(() => base.DispatchEventAsync(eventHandlerId, eventFieldInfo, args));

    private static Task UnwrapTask(Task task)
    {
        // This should always be run synchronously
        Assert.True(task.IsCompleted);
        if (task.IsFaulted)
        {
            var exception = task.Exception.Flatten().InnerException;

            while (exception is AggregateException e)
            {
                exception = e.InnerException;
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return task;
    }

    public IComponent InstantiateComponent<T>() => InstantiateComponent(typeof(T));

    protected override void HandleException(Exception exception)
    {
        if (!ShouldHandleExceptions)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        HandledExceptions.Add(exception);
        OnExceptionHandled?.Invoke();
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        OnUpdateDisplay?.Invoke(renderBatch);

        var capturedBatch = new CapturedBatch();
        Batches.Add(capturedBatch);

        for (int i = 0; i < renderBatch.UpdatedComponents.Count; i++)
        {
            ref var renderTreeDiff = ref renderBatch.UpdatedComponents.Array[i];
            capturedBatch.AddDiff(renderTreeDiff);
        }

        // Clone other data, as underlying storage will get reused by later batches
        capturedBatch.ReferenceFrames = renderBatch.ReferenceFrames.AsEnumerable().ToArray();
        capturedBatch.DisposedComponentIDs = renderBatch.DisposedComponentIDs.AsEnumerable().ToList();

        // This renderer updates the UI synchronously, like the WebAssembly one.
        // To test async UI updates, subclass TestRenderer and override UpdateDisplayAsync.

        OnUpdateDisplayComplete?.Invoke();
        return NextRenderResultTask;
    }

    public new void ProcessPendingRender() => base.ProcessPendingRender();

    public static TestRenderer CreateGenericTestRenderer()
    {
        var serviceProvider = new ServiceCollection()
            .AddTransient((sp) => Mock.Of<IJSRuntime>())
            .BuildServiceProvider();

        var testRenderder = new TestRenderer(serviceProvider);

        return testRenderder;
    }
}