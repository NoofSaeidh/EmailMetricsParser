using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog.Formatting.Compact.Reader;
using System;

string logFile = "c:\\temp\\metric2.clef";

using var clef = File.OpenText(logFile);

using var reader = new LogEventReader(clef);

var metrics = new List<Metric>();

while (reader.TryRead(out var evt))
{
    if (TryParseMetric(evt) is { } metric)
        metrics.Add(metric);
}

metrics.Sort((l, r) => l.Timestamp.CompareTo(r.Timestamp));

var incomingBatchProcessed = metrics.Where(m => m.Operation == Operations.IncomingEmailsBatchProcessed).ToList();

var calcedTimes = incomingBatchProcessed.Where(m => m.Parameters?.Count > 0).Select(m => m.Elapsed.TotalSeconds / m.Parameters!.Count).ToList();

// average
var avg = calcedTimes.Average();

var avgTs = TimeSpan.FromSeconds(avg);

// standard deviation
var sd = Math.Sqrt(calcedTimes.Average(v => Math.Pow(v - avg, 2)));

var sdTs = TimeSpan.FromSeconds(sd);

Console.WriteLine($"{Operations.IncomingEmailsBatchProcessed}: {avgTs} ± {sdTs}");


static Metric? TryParseMetric(LogEvent evt)
{
    if (evt.Properties["EmailMetricsLogEvent"] is ScalarValue { Value: true }
        && evt.Properties["Metric"] is StructureValue { Properties: { } metricProperties }
        && metricProperties.FirstOrDefault(p => p.Name == "Operation")?.Value is ScalarValue { Value: string operation }
        && metricProperties.FirstOrDefault(p => p.Name == "Elapsed")?.Value is ScalarValue { Value: string elapsedString }
        && TimeSpan.TryParse(elapsedString, out var elapsed)
        && metricProperties.FirstOrDefault(p => p.Name == "Parameters")?.Value is StructureValue { Properties: { } parameters })
    {
        return new Metric(evt.Timestamp, operation, elapsed, parameters.ToDictionary(p => p.Name, p => p.Value is ScalarValue { Value: { } value } ? value : null));
    }

    return null;
}

public record Metric(DateTimeOffset Timestamp, string Operation, TimeSpan Elapsed, IReadOnlyDictionary<string, object?>? Parameters);

public static class Operations
{
    public const string SingleOutgoingEmailSent = nameof(SingleOutgoingEmailSent);
    public const string SingleIncomingEmailRead = nameof(SingleIncomingEmailRead);
    public const string SingleIncomingEmailProcessed = nameof(SingleIncomingEmailProcessed);
    public const string IncomingEmailsBatchProcessed = nameof(IncomingEmailsBatchProcessed);
    public const string OutgoingEmailsBatchSent = nameof(OutgoingEmailsBatchSent);
}