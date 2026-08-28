// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Observability.Metrics;

/// <summary>
/// Interface for accessing current metric values.
/// </summary>
public interface IMetricsRegistry
{
    /// <summary>
    /// Gets the current value of a metric.
    /// </summary>
    double? GetCurrentValue(string metricName);

    /// <summary>
    /// Gets all current metric values.
    /// </summary>
    IReadOnlyDictionary<string, double> GetAllCurrentValues();

    /// <summary>
    /// Increments the total HTTP requests metric.
    /// </summary>
    void IncrementHttpRequests();

    /// <summary>
    /// Increments the failed HTTP requests metric.
    /// </summary>
    void IncrementHttpRequestsFailed();
}