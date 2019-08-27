﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Diagnostics
{
    using System;
    using Microsoft.Psi.Components;

    /// <summary>
    /// Component that periodically samples and produces a stream of collected diagnostics information from a running pipeline; including graph structure and message flow statistics.
    /// </summary>
    internal class DiagnosticsSampler : ISourceComponent, IDisposable
    {
        private readonly Pipeline pipeline;
        private readonly DiagnosticsCollector collector;
        private Time.TimerDelegate timerDelegate;
        private bool running;
        private Platform.ITimer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsSampler"/> class.
        /// </summary>
        /// <param name="pipeline">Pipeline to which this component belongs.</param>
        /// <param name="collector">Diagnostics collector.</param>
        /// <param name="config">Diagnostics configuration.</param>
        public DiagnosticsSampler(Pipeline pipeline, DiagnosticsCollector collector, DiagnosticsConfiguration config)
        {
            this.pipeline = pipeline;
            this.collector = collector;
            this.Config = config;
            this.Diagnostics = pipeline.CreateEmitter<PipelineDiagnostics>(this, nameof(this.Diagnostics));
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="DiagnosticsSampler"/> class.
        /// Releases underlying unmanaged timer.
        /// </summary>
        ~DiagnosticsSampler()
        {
            if (this.running)
            {
                this.timer.Stop();
            }
        }

        /// <summary>
        /// Gets emitter producing pipeline diagnostics information.
        /// </summary>
        public Emitter<PipelineDiagnostics> Diagnostics { get; private set; }

        /// <summary>
        /// Gets the diagnostics configuration.
        /// </summary>
        public DiagnosticsConfiguration Config { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Stop();
        }

        /// <inheritdoc />
        public void Start(Action<DateTime> notifyCompletionTime)
        {
            // notify that this is an infinite source component
            notifyCompletionTime(DateTime.MaxValue);
            if (this.collector != null)
            {
                this.timerDelegate = new Time.TimerDelegate((i, m, c, d1, d2) => this.Update());
                this.timer = Platform.Specific.TimerStart((uint)this.Config.SamplingInterval.TotalMilliseconds, this.timerDelegate);
                this.running = true;
            }
        }

        /// <inheritdoc />
        public void Stop(DateTime finalOriginatingTime, Action notifyCompleted)
        {
            this.Stop();
            notifyCompleted();
        }

        private void Stop()
        {
            if (this.running)
            {
                this.timer.Stop();
                this.running = false;
                this.Update(); // final update even if interval hasn't elapsed
            }

            GC.SuppressFinalize(this);
        }

        private void Update()
        {
            var root = this.collector.CurrentRoot;
            if (root != null)
            {
                this.Diagnostics.Post(root, this.pipeline.GetCurrentTime());
            }
        }
    }
}