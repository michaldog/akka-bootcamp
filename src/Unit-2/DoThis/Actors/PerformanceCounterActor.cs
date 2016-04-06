﻿namespace ChartApp.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Akka.Actor;

    public class PerformanceCounterActor : UntypedActor
    {
        private readonly string _seriesName;
        private readonly Func<PerformanceCounter> _performanceCounterGenerator;
        private PerformanceCounter _counter;

        private readonly HashSet<IActorRef> _subscriptions;
        private readonly ICancelable _cancelPublishing;

        public PerformanceCounterActor(string seriesName, Func<PerformanceCounter> performanceCounterGenerator)
        {
            _seriesName = seriesName;
            _performanceCounterGenerator = performanceCounterGenerator;
            _subscriptions = new HashSet<IActorRef>();
            _cancelPublishing = new Cancelable(Context.System.Scheduler);
        }

        protected override void PreStart()
        {
            _counter = _performanceCounterGenerator();
            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.FromMilliseconds(250),
                TimeSpan.FromMilliseconds(250),
                Self,
                new Messages.GatherMetrics(),
                Self,
                _cancelPublishing);
        }

        protected override void OnReceive(object message)
        {
            if (message is Messages.GatherMetrics)
            {
                //publish latest counter value to all subscribers
                var metric = new Messages.Metric(_seriesName, _counter.NextValue());
                foreach (var sub in _subscriptions)
                    sub.Tell(metric);
            }
            else if (message is Messages.SubscribeCounter)
            {
                // add a subscription for this counter (it's parent's job to filter by counter types)
                var sc = message as Messages.SubscribeCounter;
                _subscriptions.Add(sc.Subscriber);
            }
            else if (message is Messages.UnsubscribeCounter)
            {
                // remove a subscription from this counter
                var uc = message as Messages.UnsubscribeCounter;
                _subscriptions.Remove(uc.Subscriber);
            }
        }

        protected override void PostStop()
        {
            try
            {
                //terminate the scheduled task
                _cancelPublishing.Cancel(false);
                _counter.Dispose();
            }
            catch
            {
                //don't care about additional "ObjectDisposed" exceptions
            }
            finally
            {
                base.PostStop();
            }
        }
    }
}