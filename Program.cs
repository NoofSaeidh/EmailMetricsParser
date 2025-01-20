using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog.Formatting.Compact.Reader;
using System;

string logFile = "C:\\temp\\metrics 1.clef";

using var clef = File.OpenText(logFile);

using var reader = new LogEventReader(clef);

var metrics = new List<Metric>();

while (reader.TryRead(out var evt))
{
    if (TryParseMetric(evt) is { } metric)
        metrics.Add(metric);
}

metrics.Sort((l, r) => l.Timestamp.CompareTo(r.Timestamp));

const string format1 = "{0} Average (divided by count): {1} ± {2}. Max: {3}. Min: {4}. Total count: {5}";
const string format2 = "{0} Average: {1} ± {2}. Max: {3}. Min: {4}. Total count: {5}";

Log(Operations.IncomingEmailsBatchProcessed, format1);
Log(Operations.OutgoingEmailsBatchSent, format1);
Log(Operations.SingleIncomingEmailProcessed, format2);
Log(Operations.SingleIncomingEmailRead, format2);
Log(Operations.SingleOutgoingEmailSent, format2);

void Log(string operation, string messageFormat)
{
    var data = CalcTimes(metrics, operation);
    if (data == default)
        return;
    Console.WriteLine(messageFormat, operation, data.Average, data.StandardDeviation, data.Max, data.Min, data.Count);
}


static (TimeSpan Average, TimeSpan StandardDeviation, TimeSpan Max, TimeSpan Min, int Count) CalcTimes(IEnumerable<Metric> metrics_, string operation)
{
    var times = metrics_.Where(m => m.Operation == operation).Select(m =>
    {
        if (m.Parameters?.Count is { } count) return m.Elapsed.TotalSeconds / count;
        return m.Elapsed.TotalSeconds;
    }).ToList();

    if (times.Count == 0)
        return default;

    // average
    var avg = times.Average();

    var avgTs = TimeSpan.FromSeconds(avg);

    // standard deviation
    var sd = Math.Sqrt(times.Average(v => Math.Pow(v - avg, 2)));

    var sdTs = TimeSpan.FromSeconds(sd);

    var max = TimeSpan.FromSeconds(times.Max());
    var min = TimeSpan.FromSeconds(times.Min());

    return (avgTs, sdTs, max, min, times.Count);
}


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