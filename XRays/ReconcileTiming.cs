using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace XRays
{
    /// <summary>
    /// Timing of the reconcile pass (the periodic recomputation of what has
    /// to stay hidden), for the option "Log reconcile timing": the time spent
    /// in each phase is accumulated while the option is on, and one line goes
    /// to the log about every second with the average, the smallest and the
    /// biggest value of the passes that second covered. Nothing is measured
    /// while the option is off.
    ///
    /// The first phases are laid end to end and cover the whole pass; the two
    /// "of which" phases are measured inside Categories, they are part of
    /// "roots" and they never add time to the total.
    /// </summary>
    internal static class ReconcileTiming
    {
        /// <summary>Set every frame from the config entry of the same name.</summary>
        public static bool Enabled;

        /// <summary>How often the line of the second that ran is written.</summary>
        private const float LogInterval = 1f;

        /// <summary>The phases, in the order the log lists them.</summary>
        private static readonly string[] PhaseNames =
        {
            "terrain",
            "water",
            "clouds",
            "mist",
            "player",
            "roots",
            "of which collect",
            "of which names",
            "apply"
        };

        private class Stat
        {
            public long Count;
            public double Sum;
            public double Min;
            public double Max;

            public void Add(double value)
            {
                if (Count == 0 || value < Min)
                {
                    Min = value;
                }

                if (Count == 0 || value > Max)
                {
                    Max = value;
                }

                Count++;
                Sum += value;
            }

            public void Reset()
            {
                Count = 0;
                Sum = 0;
                Min = 0;
                Max = 0;
            }
        }

        private static readonly Dictionary<string, Stat> Phases = new Dictionary<string, Stat>(
            StringComparer.Ordinal
        );

        private static readonly Stat Total = new Stat();
        private static readonly Stopwatch Watch = new Stopwatch();
        private static readonly StringBuilder Line = new StringBuilder(320);

        private static double lastMark;
        private static long passes;
        private static float windowTimer;

        static ReconcileTiming()
        {
            foreach (string name in PhaseNames)
            {
                Phases[name] = new Stat();
            }
        }

        /// <summary>Starts measuring a pass (nothing while the option is off).</summary>
        public static void Begin()
        {
            if (!Enabled)
            {
                return;
            }

            Watch.Restart();
            lastMark = 0.0;
            passes++;
        }

        /// <summary>
        /// Closes the phase that just ran: the time since the previous mark
        /// goes to that phase and to the total of the pass.
        /// </summary>
        public static void Mark(string name)
        {
            if (!Enabled || passes == 0)
            {
                return;
            }

            double now = Watch.Elapsed.TotalMilliseconds;
            double value = now - lastMark;
            lastMark = now;

            Total.Add(value);
            Of(name).Add(value);
        }

        /// <summary>
        /// A starting point for a phase measured somewhere else, 0 when the
        /// option is off (a stamp of 0 is never added to any phase).
        /// </summary>
        public static long Stamp()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        /// <summary>
        /// Adds the milliseconds elapsed since that stamp to that phase. The
        /// "of which" phases only count inside a pass: the scans the panel
        /// runs for its counters belong to no pass at all.
        /// </summary>
        public static void AddSince(string name, long stamp)
        {
            if (!Enabled || stamp == 0 || passes == 0)
            {
                return;
            }

            double elapsed = (Stopwatch.GetTimestamp() - stamp) * 1000.0 / Stopwatch.Frequency;
            Of(name).Add(elapsed);
        }

        /// <summary>Writes the line of the second that just ran, then opens a new window.</summary>
        public static void Tick(float deltaTime)
        {
            if (!Enabled)
            {
                // without a window in progress there is nothing to drop: the
                // counters are only cleared when the option went off in the
                // middle of one
                if (windowTimer > 0f || passes > 0)
                {
                    Reset();
                }

                return;
            }

            windowTimer += deltaTime;
            if (windowTimer < LogInterval)
            {
                return;
            }

            if (passes > 0)
            {
                Write(windowTimer);
            }

            Reset();
        }

        private static Stat Of(string name)
        {
            Stat stat;
            if (!Phases.TryGetValue(name, out stat))
            {
                stat = new Stat();
                Phases[name] = stat;
            }

            return stat;
        }

        private static void Reset()
        {
            windowTimer = 0f;
            passes = 0;
            lastMark = 0.0;
            Total.Reset();

            foreach (string name in PhaseNames)
            {
                Phases[name].Reset();
            }
        }

        private static void Write(float seconds)
        {
            Line.Length = 0;
            Line.Append("Reconcile timing over ");
            Line.Append(seconds.ToString("0.0", CultureInfo.InvariantCulture));
            Line.Append(" s (");
            Line.Append(passes);
            Line.Append(passes == 1 ? " pass), all in ms: " : " passes), all in ms: ");
            Append(Line, "total", Total);

            foreach (string name in PhaseNames)
            {
                Stat stat = Phases[name];
                if (stat.Count == 0)
                {
                    continue;
                }

                Line.Append(" | ");
                Append(Line, name, stat);
            }

            XRays.Instance?.Log.LogInfo(Line.ToString());
        }

        private static void Append(StringBuilder builder, string name, Stat stat)
        {
            builder.Append(name);
            builder.Append(' ');
            builder.Append((stat.Sum / stat.Count).ToString("0.0", CultureInfo.InvariantCulture));
            builder.Append(" (");
            builder.Append(stat.Min.ToString("0.0", CultureInfo.InvariantCulture));
            builder.Append('-');
            builder.Append(stat.Max.ToString("0.0", CultureInfo.InvariantCulture));
            builder.Append(')');
        }
    }
}
