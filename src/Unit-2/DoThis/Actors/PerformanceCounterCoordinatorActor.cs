namespace ChartApp.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Windows.Forms.DataVisualization.Charting;
    using Akka.Actor;

    public class PerformanceCounterCoordinatorActor : ReceiveActor
    {
        public class Watch
        {
            public Watch(Messages.CounterType counter)
            {
                Counter = counter;
            }

            public Messages.CounterType Counter { get; private set; }
        }

        public class Unwatch
        {
            public Unwatch(Messages.CounterType counter)
            {
                Counter = counter;
            }

            public Messages.CounterType Counter { get; private set; }
        }

        private static readonly Dictionary<Messages.CounterType, Func<PerformanceCounter>> CounterGenerators = new Dictionary<Messages.CounterType, Func<PerformanceCounter>>
        {
            {Messages.CounterType.Cpu, () => new PerformanceCounter("Processor", "% Processor Time", "_Total", true)},
            {Messages.CounterType.Memory, () => new PerformanceCounter("Memory", "% Committed Bytes In Use", true)},
            {Messages.CounterType.Disk, () => new PerformanceCounter("LogicalDisk", "% Disk Time", "_Total", true)}
        };

        private static readonly Dictionary<Messages.CounterType, Func<Series>> CounterSeries = new Dictionary<Messages.CounterType, Func<Series>>()
        {
            {Messages.CounterType.Cpu, () => new Series(Messages.CounterType.Cpu.ToString()) {ChartType = SeriesChartType.SplineArea, Color = Color.DarkGreen}},
            {Messages.CounterType.Memory, () => new Series(Messages.CounterType.Memory.ToString()) {ChartType = SeriesChartType.FastLine, Color = Color.MediumBlue}},
            {Messages.CounterType.Disk, () => new Series(Messages.CounterType.Disk.ToString()) {ChartType = SeriesChartType.SplineArea, Color = Color.DarkRed}}
        };

        private Dictionary<Messages.CounterType, IActorRef> _counterActors;

        private IActorRef _chartingActor;

        public PerformanceCounterCoordinatorActor(IActorRef chartingActor) : this(chartingActor, new Dictionary<Messages.CounterType, IActorRef>())
        {
        }

        public PerformanceCounterCoordinatorActor(IActorRef chartingActor, Dictionary<Messages.CounterType, IActorRef> counterActors)
        {
            _chartingActor = chartingActor;
            _counterActors = counterActors;

            Receive<Watch>(x =>
            {
                if (!_counterActors.ContainsKey(x.Counter))
                {
                    var counterActor = Context.ActorOf(Props.Create(() => new PerformanceCounterActor(x.Counter.ToString(), CounterGenerators[x.Counter])));
                    _counterActors[x.Counter] = counterActor;
                }

                _chartingActor.Tell(new ChartingActor.AddSeries(CounterSeries[x.Counter]()));

                _counterActors[x.Counter].Tell(new Messages.SubscribeCounter(x.Counter, _chartingActor));
            });

            Receive<Unwatch>(x =>
            {
                if (!_counterActors.ContainsKey(x.Counter))
                    return;

                _counterActors[x.Counter].Tell(new Messages.UnsubscribeCounter(x.Counter, _chartingActor));

                // remove this series from the ChartingActor
                _chartingActor.Tell(new ChartingActor.RemoveSeries(x.Counter.ToString()));
            });
        }
    }
}