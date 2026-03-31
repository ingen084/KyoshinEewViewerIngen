using System;
using Prometheus;

namespace ReplayGenerator.Observability;

public static class ReplayMetrics
{
	private static readonly Counter GenerationsTotal = global::Prometheus.Metrics.CreateCounter(
		"replay_generator_generations_total",
		"Number of replay generation attempts by trigger and result",
		new CounterConfiguration { LabelNames = ["trigger", "result"] });

	private static readonly Histogram GenerationDurationSeconds = global::Prometheus.Metrics.CreateHistogram(
		"replay_generator_generation_duration_seconds",
		"Wall time to build, upload, and persist a replay file",
		new HistogramConfiguration { LabelNames = ["trigger"] });

	public static void RecordGeneration(string trigger, bool success, TimeSpan duration)
	{
		GenerationsTotal.WithLabels(trigger, success ? "success" : "failure").Inc();
		GenerationDurationSeconds.WithLabels(trigger).Observe(duration.TotalSeconds);
	}
}
