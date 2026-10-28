using System.ComponentModel;
using System.Runtime.CompilerServices;
using MGUI.Core.UI.Data_Binding;

namespace MGUI.Tests.Bindings;

[Collection("Render cache clock")]
public sealed class DataBindingLifetimeTests
{
    [Fact]
    public void BulkRemovalClearsAllRegistriesAndAllowsRebinding()
    {
        int before = DataBindingManager.Bindings.Count;
        Model target = new();
        Model source = new() { Value = "initial" };
        BindingConfig config = Config(source);
        DataBinding first = DataBindingManager.AddBinding(config, target);
        DataBinding second = DataBindingManager.AddBinding(config with { TargetPath = nameof(Model.Other) }, target);
        Assert.Equal("initial", target.Value);
        Assert.Equal(2, DataBindingManager.RemoveBindings(target));
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
        Assert.Equal(before, DataBindingManager.Bindings.Count);
        Assert.Equal(0, DataBindingManager.RemoveBindings(target));
        source.Value = "removed";
        Assert.Equal("initial", target.Value);
        using DataBinding rebound = DataBindingManager.AddBinding(config, target);
        Assert.Equal("removed", target.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleRemovalAndDirectDisposalUnregisterNestedTargets(bool disposeDirectly)
    {
        int before = DataBindingManager.Bindings.Count;
        TargetRoot root = new();
        BindingConfig config = Config(new Model()) with { TargetPath = "Child.Value" };
        DataBinding binding = DataBindingManager.AddBinding(config, root);
        if (disposeDirectly)
        {
            binding.Dispose();
        }
        else
        {
            Assert.True(DataBindingManager.RemoveBinding(binding));
        }
        binding.Dispose();
        Assert.False(DataBindingManager.RemoveBinding(binding));
        Assert.Equal(0, DataBindingManager.RemoveBindings(root));
        Assert.Equal(before, DataBindingManager.Bindings.Count);
        using DataBinding rebound = DataBindingManager.AddBinding(config, root);
    }

    [Fact]
    public void ContextReplacementWorksUntilDisposalAndDoesNotResubscribeAfterwards()
    {
        Model oldSource = new() { Value = "old" };
        ContextHost host = new() { DataContext = oldSource };
        Model target = new();
        DataBinding binding = DataBindingManager.AddBinding(Config(host, true) with { SourcePath = "Child.Value" }, target);
        oldSource.Child = new() { Value = "nested" };
        Assert.Equal("nested", target.Value);
        Model newSource = new() { Child = new() { Value = "new" } };
        host.DataContext = newSource;
        Assert.Equal("new", target.Value);
        Assert.Equal(0, oldSource.SubscriberCount);
        Assert.Equal(1, host.SubscriberCount);
        binding.Dispose();
        Assert.Equal(0, host.SubscriberCount);
        Assert.Equal(0, newSource.SubscriberCount);
        Assert.Equal(0, newSource.Child.SubscriberCount);
        host.DataContext = new Model() { Child = new() { Value = "later" } };
        Assert.Equal("new", target.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedBindingsAndTargetsAreCollectibleWithSourceStillRooted(bool useContext)
    {
        object source = useContext ? new ContextHost() { DataContext = new Model() } : new Model();
        (WeakReference binding, WeakReference target) = BindAndRemove(source, useContext);
        Collect();
        Assert.False(binding.IsAlive);
        Assert.False(target.IsAlive);
        GC.KeepAlive(source);
    }

    [Fact]
    public void RejectedDuplicateDoesNotAddSubscriptionsOrRegistryEntries()
    {
        Model source = new();
        Model target = new();
        BindingConfig config = Config(source);
        using DataBinding binding = DataBindingManager.AddBinding(config, target);
        int before = DataBindingManager.Bindings.Count;
        Assert.Throws<InvalidOperationException>(() => DataBindingManager.AddBinding(config, target));
        Assert.Equal(before, DataBindingManager.Bindings.Count);
        Assert.Equal(1, source.SubscriberCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, WeakReference) BindAndRemove(object source, bool useContext)
    {
        Model target = new();
        DataBinding binding = DataBindingManager.AddBinding(Config(source, useContext), target);
        DataBindingManager.RemoveBindings(target);
        return (new(binding), new(target));
    }

    private static BindingConfig Config(object source, bool useContext = false) => new(
        nameof(Model.Value), nameof(Model.Value), SourceResolver: new FixedResolver(source),
        DataContextResolver: useContext ? DataContextResolver.DataContext : DataContextResolver.Self);

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed record FixedResolver(object Source) : ISourceObjectResolver
    {
        public object ResolveSourceObject(object target) => Source;
    }

    public sealed class TargetRoot
    {
        public Model Child { get; } = new();
    }

    public sealed class Model : INotifyPropertyChanged
    {
        private string? value;
        private Model? child;
        public string? Value
        {
            get => value;
            set { this.value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); }
        }
        public Model? Child
        {
            get => child;
            set { child = value; PropertyChanged?.Invoke(this, new(nameof(Child))); }
        }
        public string? Other { get; set; }
        public int SubscriberCount => PropertyChanged?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed class ContextHost : IObservableDataContext
    {
        private object? dataContext;
        public object? DataContext
        {
            get => dataContext;
            set { dataContext = value; DataContextChanged?.Invoke(this, value!); }
        }
        public int SubscriberCount => DataContextChanged?.GetInvocationList().Length ?? 0;
        public event EventHandler<object>? DataContextChanged;
    }
}
