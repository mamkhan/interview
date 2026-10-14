using AwesomeAssertions;
using BuildingBlocks.Persistence;
using BuildingBlocks.Seedwork;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.UnitTests;

// Domain events are handled in-process inside the unit of work: a tracked aggregate's events reach every registered handler in
// the scope, a handler may raise on another tracked aggregate and be heard, and a chain that never
// settles fails instead of hanging.
public class WhenDispatchingDomainEvents
{
    [Fact]
    public async Task DeliversEachEventOnceToEveryRegisteredHandler()
    {
        var heard = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(heard);
        services.AddScoped<IDomainEventHandler<Counted>, RecordingHandler>();
        services.AddScoped<IDomainEventHandler<Counted>, RecordingHandlerTwo>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var counter = new Counter("a");
        counter.Increment();
        counter.Increment();

        var dispatched = await new DomainEventDispatcher(scope.ServiceProvider).DispatchAsync([counter], TestContext.Current.CancellationToken);

        dispatched.Should().Be(4);
        heard.Should().BeEquivalentTo(["one:a:1", "two:a:1", "one:a:2", "two:a:2"]);
        counter.DomainEvents.Should().BeEmpty("dispatch dequeues what it delivered");
    }

    [Fact]
    public async Task HearsAHandlerThatRaisesOnAnotherTrackedAggregateInTheNextRound()
    {
        var heard = new List<string>();
        var mirror = new Counter("mirror");
        var services = new ServiceCollection();
        services.AddSingleton(heard);
        services.AddSingleton(mirror);
        services.AddScoped<IDomainEventHandler<Counted>, MirroringHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var source = new Counter("source");
        source.Increment();

        await new DomainEventDispatcher(scope.ServiceProvider).DispatchAsync([source, mirror], TestContext.Current.CancellationToken);

        heard.Should().Equal("mirror:source:1", "mirror:mirror:1");
    }

    [Fact]
    public async Task FailsAChainThatNeverSettlesInsteadOfHanging()
    {
        var counter = new Counter("loop");
        var services = new ServiceCollection();
        services.AddSingleton(counter);
        services.AddScoped<IDomainEventHandler<Counted>, EndlessHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        counter.Increment();

        var act = () => new DomainEventDispatcher(scope.ServiceProvider).DispatchAsync([counter], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{DomainEventDispatcher.MaxRounds} rounds*");
    }

    public sealed record Counted(string Name, int Value) : IDomainEvent;

    public sealed class Counter(string name) : AggregateRoot<string>(name)
    {
        private int _value;

        public void Increment() => Raise(new Counted(Id, ++_value));
    }

    private sealed class RecordingHandler(List<string> heard) : IDomainEventHandler<Counted>
    {
        public Task HandleAsync(Counted domainEvent, CancellationToken cancellationToken)
        {
            heard.Add($"one:{domainEvent.Name}:{domainEvent.Value}");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandlerTwo(List<string> heard) : IDomainEventHandler<Counted>
    {
        public Task HandleAsync(Counted domainEvent, CancellationToken cancellationToken)
        {
            heard.Add($"two:{domainEvent.Name}:{domainEvent.Value}");
            return Task.CompletedTask;
        }
    }

    private sealed class MirroringHandler(List<string> heard, Counter mirror) : IDomainEventHandler<Counted>
    {
        public Task HandleAsync(Counted domainEvent, CancellationToken cancellationToken)
        {
            heard.Add($"mirror:{domainEvent.Name}:{domainEvent.Value}");
            if (domainEvent.Name != mirror.Id)
            {
                mirror.Increment();
            }

            return Task.CompletedTask;
        }
    }

    // Raising on the aggregate that produced the event, every time, is the loop the round limit guards.
    private sealed class EndlessHandler(Counter counter) : IDomainEventHandler<Counted>
    {
        public Task HandleAsync(Counted domainEvent, CancellationToken cancellationToken)
        {
            counter.Increment();
            return Task.CompletedTask;
        }
    }
}
