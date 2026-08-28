// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace KKBOT.Observability.Metrics;

/// <summary>
/// Central registry for all KKBOT metrics with current value tracking.
/// </summary>
public sealed class MetricsRegistry : IMetricsRegistry, IDisposable
{
    private readonly Meter _meter;
    private readonly ConcurrentDictionary<string, double> _currentValues = new();
    private bool _disposed;

    // Instance metrics
    public Counter<long> InstancesTotal { get; }
    public Counter<long> InstancesStarted { get; }
    public Counter<long> InstancesStopped { get; }
    public Counter<long> InstancesFailed { get; }
    public UpDownCounter<long> InstancesRunning { get; }
    public UpDownCounter<long> InstancesPaused { get; }
    public Histogram<double> InstanceStartupDurationMs { get; }
    public Histogram<double> InstanceCpuUsagePercent { get; }
    public Histogram<double> InstanceMemoryUsageMb { get; }

    // Job/Task metrics
    public Counter<long> JobsTotal { get; }
    public Counter<long> JobsCompleted { get; }
    public Counter<long> JobsFailed { get; }
    public Counter<long> JobsCancelled { get; }
    public UpDownCounter<long> JobsRunning { get; }
    public Histogram<double> JobDurationMs { get; }
    public Histogram<double> JobQueueDepth { get; }
    public Histogram<double> TaskDurationMs { get; }
    public Counter<long> TaskRetries { get; }

    // Device metrics
    public Counter<long> DeviceCapturesTotal { get; }
    public Counter<long> DeviceCapturesFailed { get; }
    public Histogram<double> DeviceCaptureLatencyMs { get; }
    public Counter<long> DeviceInputsTotal { get; }
    public Counter<long> DeviceInputsFailed { get; }
    public Histogram<double> DeviceInputLatencyMs { get; }
    public UpDownCounter<long> DevicesConnected { get; }

    // Perception metrics
    public Counter<long> DetectionsTotal { get; }
    public Counter<long> DetectionsMatched { get; }
    public Histogram<double> DetectionConfidence { get; }
    public Histogram<double> DetectionLatencyMs { get; }
    public Counter<long> OcrRequestsTotal { get; }
    public Counter<long> OcrRequestsFailed { get; }
    public Histogram<double> OcrLatencyMs { get; }
    public Histogram<double> OcrConfidence { get; }

    // AI metrics
    public Counter<long> AiInferencesTotal { get; }
    public Counter<long> AiInferencesFailed { get; }
    public Histogram<double> AiInferenceLatencyMs { get; }
    public Histogram<double> AiTokensPerRequest { get; }
    public Histogram<double> AiTokensPerSecond { get; }
    public Counter<long> AiStreamingChunks { get; }

    // Plugin metrics
    public Counter<long> PluginLoadsTotal { get; }
    public Counter<long> PluginLoadsFailed { get; }
    public Histogram<double> PluginLoadDurationMs { get; }
    public UpDownCounter<long> PluginsLoaded { get; }
    public Counter<long> PluginExecutionsTotal { get; }
    public Counter<long> PluginExecutionsFailed { get; }
    public Histogram<double> PluginExecutionLatencyMs { get; }

    // Orchestration metrics
    public Counter<long> StateTransitionsTotal { get; }
    public Counter<long> RecoveryAttemptsTotal { get; }
    public Counter<long> RecoverySuccesses { get; }
    public Counter<long> RecoveryFailures { get; }
    public Histogram<double> RecoveryDurationMs { get; }
    public Histogram<double> DagExecutionDurationMs { get; }
    public Counter<long> SagaExecutionsTotal { get; }
    public Counter<long> SagaCompensationsTotal { get; }

    // Marketplace metrics
    public Counter<long> MarketplaceDownloadsTotal { get; }
    public Counter<long> MarketplaceInstallsTotal { get; }
    public Counter<long> MarketplacePublishesTotal { get; }
    public Counter<long> MarketplaceSecurityScansTotal { get; }
    public Counter<long> MarketplaceSecurityIssuesFound { get; }
    public Histogram<double> MarketplacePluginRating { get; }

    // Collaboration metrics
    public UpDownCounter<long> ActiveSessions { get; }
    public UpDownCounter<long> ConnectedUsers { get; }
    public Counter<long> MessagesSent { get; }
    public Counter<long> RecordingsStarted { get; }
    public Counter<long> ScreenSharesStarted { get; }

    // HTTP/External metrics
    public Counter<long> HttpRequestsTotal { get; }
    public Counter<long> HttpRequestsFailed { get; }
    public Histogram<double> HttpRequestDurationMs { get; }
    public Histogram<double> HttpRequestSizeBytes { get; }
    public Histogram<double> HttpResponseSizeBytes { get; }

    public MetricsRegistry()
    {
        _meter = new Meter("KKBOT", "1.0.0");

        // Instance metrics
        InstancesTotal = CreateCounter("kkbot_instances_total", "Total instances created");
        InstancesStarted = CreateCounter("kkbot_instances_started_total", "Total instances started");
        InstancesStopped = CreateCounter("kkbot_instances_stopped_total", "Total instances stopped");
        InstancesFailed = CreateCounter("kkbot_instances_failed_total", "Total instances failed");
        InstancesRunning = CreateUpDownCounter("kkbot_instances_running", "Currently running instances");
        InstancesPaused = CreateUpDownCounter("kkbot_instances_paused", "Currently paused instances");
        InstanceStartupDurationMs = CreateHistogram("kkbot_instance_startup_duration_ms", "Instance startup duration in milliseconds");
        InstanceCpuUsagePercent = CreateHistogram("kkbot_instance_cpu_usage_percent", "Instance CPU usage percentage");
        InstanceMemoryUsageMb = CreateHistogram("kkbot_instance_memory_usage_mb", "Instance memory usage in MB");

        // Job/Task metrics
        JobsTotal = CreateCounter("kkbot_jobs_total", "Total jobs created");
        JobsCompleted = CreateCounter("kkbot_jobs_completed_total", "Total jobs completed");
        JobsFailed = CreateCounter("kkbot_jobs_failed_total", "Total jobs failed");
        JobsCancelled = CreateCounter("kkbot_jobs_cancelled_total", "Total jobs cancelled");
        JobsRunning = CreateUpDownCounter("kkbot_jobs_running", "Currently running jobs");
        JobDurationMs = CreateHistogram("kkbot_job_duration_ms", "Job execution duration in milliseconds");
        JobQueueDepth = CreateHistogram("kkbot_job_queue_depth", "Job queue depth");
        TaskDurationMs = CreateHistogram("kkbot_task_duration_ms", "Task execution duration in milliseconds");
        TaskRetries = CreateCounter("kkbot_task_retries_total", "Total task retries");

        // Device metrics
        DeviceCapturesTotal = CreateCounter("kkbot_device_captures_total", "Total device screen captures");
        DeviceCapturesFailed = CreateCounter("kkbot_device_captures_failed_total", "Failed device captures");
        DeviceCaptureLatencyMs = CreateHistogram("kkbot_device_capture_latency_ms", "Device capture latency in milliseconds");
        DeviceInputsTotal = CreateCounter("kkbot_device_inputs_total", "Total device input commands");
        DeviceInputsFailed = CreateCounter("kkbot_device_inputs_failed_total", "Failed device inputs");
        DeviceInputLatencyMs = CreateHistogram("kkbot_device_input_latency_ms", "Device input latency in milliseconds");
        DevicesConnected = CreateUpDownCounter("kkbot_devices_connected", "Currently connected devices");

        // Perception metrics
        DetectionsTotal = CreateCounter("kkbot_detections_total", "Total detections performed");
        DetectionsMatched = CreateCounter("kkbot_detections_matched_total", "Total detections that matched");
        DetectionConfidence = CreateHistogram("kkbot_detection_confidence", "Detection confidence score");
        DetectionLatencyMs = CreateHistogram("kkbot_detection_latency_ms", "Detection latency in milliseconds");
        OcrRequestsTotal = CreateCounter("kkbot_ocr_requests_total", "Total OCR requests");
        OcrRequestsFailed = CreateCounter("kkbot_ocr_requests_failed_total", "Failed OCR requests");
        OcrLatencyMs = CreateHistogram("kkbot_ocr_latency_ms", "OCR latency in milliseconds");
        OcrConfidence = CreateHistogram("kkbot_ocr_confidence", "OCR confidence score");

        // AI metrics
        AiInferencesTotal = CreateCounter("kkbot_ai_inferences_total", "Total AI inferences");
        AiInferencesFailed = CreateCounter("kkbot_ai_inferences_failed_total", "Failed AI inferences");
        AiInferenceLatencyMs = CreateHistogram("kkbot_ai_inference_latency_ms", "AI inference latency in milliseconds");
        AiTokensPerRequest = CreateHistogram("kkbot_ai_tokens_per_request", "Tokens per AI request");
        AiTokensPerSecond = CreateHistogram("kkbot_ai_tokens_per_second", "Tokens per second during inference");
        AiStreamingChunks = CreateCounter("kkbot_ai_streaming_chunks_total", "Total streaming chunks emitted");

        // Plugin metrics
        PluginLoadsTotal = CreateCounter("kkbot_plugin_loads_total", "Total plugin loads");
        PluginLoadsFailed = CreateCounter("kkbot_plugin_loads_failed_total", "Failed plugin loads");
        PluginLoadDurationMs = CreateHistogram("kkbot_plugin_load_duration_ms", "Plugin load duration in milliseconds");
        PluginsLoaded = CreateUpDownCounter("kkbot_plugins_loaded", "Currently loaded plugins");
        PluginExecutionsTotal = CreateCounter("kkbot_plugin_executions_total", "Total plugin task executions");
        PluginExecutionsFailed = CreateCounter("kkbot_plugin_executions_failed_total", "Failed plugin executions");
        PluginExecutionLatencyMs = CreateHistogram("kkbot_plugin_execution_latency_ms", "Plugin execution latency in milliseconds");

        // Orchestration metrics
        StateTransitionsTotal = CreateCounter("kkbot_state_transitions_total", "Total state transitions");
        RecoveryAttemptsTotal = CreateCounter("kkbot_recovery_attempts_total", "Total recovery attempts");
        RecoverySuccesses = CreateCounter("kkbot_recovery_successes_total", "Successful recoveries");
        RecoveryFailures = CreateCounter("kkbot_recovery_failures_total", "Failed recoveries");
        RecoveryDurationMs = CreateHistogram("kkbot_recovery_duration_ms", "Recovery duration in milliseconds");
        DagExecutionDurationMs = CreateHistogram("kkbot_dag_execution_duration_ms", "DAG execution duration in milliseconds");
        SagaExecutionsTotal = CreateCounter("kkbot_saga_executions_total", "Total saga executions");
        SagaCompensationsTotal = CreateCounter("kkbot_saga_compensations_total", "Total saga compensations");

        // Marketplace metrics
        MarketplaceDownloadsTotal = CreateCounter("kkbot_marketplace_downloads_total", "Total plugin downloads");
        MarketplaceInstallsTotal = CreateCounter("kkbot_marketplace_installs_total", "Total plugin installs");
        MarketplacePublishesTotal = CreateCounter("kkbot_marketplace_publishes_total", "Total plugin publishes");
        MarketplaceSecurityScansTotal = CreateCounter("kkbot_marketplace_security_scans_total", "Total security scans");
        MarketplaceSecurityIssuesFound = CreateCounter("kkbot_marketplace_security_issues_found_total", "Security issues found");
        MarketplacePluginRating = CreateHistogram("kkbot_marketplace_plugin_rating", "Plugin rating (1-5)");

        // Collaboration metrics
        ActiveSessions = CreateUpDownCounter("kkbot_active_sessions", "Active collaboration sessions");
        ConnectedUsers = CreateUpDownCounter("kkbot_connected_users", "Connected users");
        MessagesSent = CreateCounter("kkbot_messages_sent_total", "Total messages sent");
        RecordingsStarted = CreateCounter("kkbot_recordings_started_total", "Recordings started");
        ScreenSharesStarted = CreateCounter("kkbot_screen_shares_started_total", "Screen shares started");

        // HTTP metrics
        HttpRequestsTotal = CreateCounter("kkbot_http_requests_total", "Total HTTP requests");
        HttpRequestsFailed = CreateCounter("kkbot_http_requests_failed_total", "Failed HTTP requests");
        HttpRequestDurationMs = CreateHistogram("kkbot_http_request_duration_ms", "HTTP request duration in milliseconds");
        HttpRequestSizeBytes = CreateHistogram("kkbot_http_request_size_bytes", "HTTP request size in bytes");
        HttpResponseSizeBytes = CreateHistogram("kkbot_http_response_size_bytes", "HTTP response size in bytes");
    }

    private Counter<long> CreateCounter(string name, string description)
    {
        var counter = _meter.CreateCounter<long>(name, description: description);
        _currentValues[name] = 0;
        return counter;
    }

    private UpDownCounter<long> CreateUpDownCounter(string name, string description)
    {
        var counter = _meter.CreateUpDownCounter<long>(name, description: description);
        _currentValues[name] = 0;
        return counter;
    }

    private Histogram<double> CreateHistogram(string name, string description)
    {
        var histogram = _meter.CreateHistogram<double>(name, description: description);
        _currentValues[name] = 0;
        return histogram;
    }

    // Wrapper methods that track current values
    public void IncrementInstanceTotal() => TrackAndIncrement(InstancesTotal, "kkbot_instances_total", 1);
    public void IncrementInstancesStarted() => TrackAndIncrement(InstancesStarted, "kkbot_instances_started_total", 1);
    public void IncrementInstancesStopped() => TrackAndIncrement(InstancesStopped, "kkbot_instances_stopped_total", 1);
    public void IncrementInstancesFailed() => TrackAndIncrement(InstancesFailed, "kkbot_instances_failed_total", 1);
    public void SetInstancesRunning(long value) => TrackAndSet(InstancesRunning, "kkbot_instances_running", value);
    public void SetInstancesPaused(long value) => TrackAndSet(InstancesPaused, "kkbot_instances_paused", value);
    public void RecordInstanceStartupDuration(double ms) => TrackAndRecord(InstanceStartupDurationMs, "kkbot_instance_startup_duration_ms", ms);
    public void RecordInstanceCpuUsage(double percent) => TrackAndRecord(InstanceCpuUsagePercent, "kkbot_instance_cpu_usage_percent", percent);
    public void RecordInstanceMemoryUsage(double mb) => TrackAndRecord(InstanceMemoryUsageMb, "kkbot_instance_memory_usage_mb", mb);

    public void IncrementJobsTotal() => TrackAndIncrement(JobsTotal, "kkbot_jobs_total", 1);
    public void IncrementJobsCompleted() => TrackAndIncrement(JobsCompleted, "kkbot_jobs_completed_total", 1);
    public void IncrementJobsFailed() => TrackAndIncrement(JobsFailed, "kkbot_jobs_failed_total", 1);
    public void IncrementJobsCancelled() => TrackAndIncrement(JobsCancelled, "kkbot_jobs_cancelled_total", 1);
    public void SetJobsRunning(long value) => TrackAndSet(JobsRunning, "kkbot_jobs_running", value);
    public void RecordJobDuration(double ms) => TrackAndRecord(JobDurationMs, "kkbot_job_duration_ms", ms);
    public void RecordJobQueueDepth(double depth) => TrackAndRecord(JobQueueDepth, "kkbot_job_queue_depth", depth);
    public void RecordTaskDuration(double ms) => TrackAndRecord(TaskDurationMs, "kkbot_task_duration_ms", ms);
    public void IncrementTaskRetries() => TrackAndIncrement(TaskRetries, "kkbot_task_retries_total", 1);

    public void IncrementDeviceCaptures() => TrackAndIncrement(DeviceCapturesTotal, "kkbot_device_captures_total", 1);
    public void IncrementDeviceCapturesFailed() => TrackAndIncrement(DeviceCapturesFailed, "kkbot_device_captures_failed_total", 1);
    public void RecordDeviceCaptureLatency(double ms) => TrackAndRecord(DeviceCaptureLatencyMs, "kkbot_device_capture_latency_ms", ms);
    public void IncrementDeviceInputs() => TrackAndIncrement(DeviceInputsTotal, "kkbot_device_inputs_total", 1);
    public void IncrementDeviceInputsFailed() => TrackAndIncrement(DeviceInputsFailed, "kkbot_device_inputs_failed_total", 1);
    public void RecordDeviceInputLatency(double ms) => TrackAndRecord(DeviceInputLatencyMs, "kkbot_device_input_latency_ms", ms);
    public void SetDevicesConnected(long value) => TrackAndSet(DevicesConnected, "kkbot_devices_connected", value);

    public void IncrementDetections() => TrackAndIncrement(DetectionsTotal, "kkbot_detections_total", 1);
    public void IncrementDetectionsMatched() => TrackAndIncrement(DetectionsMatched, "kkbot_detections_matched_total", 1);
    public void RecordDetectionConfidence(double confidence) => TrackAndRecord(DetectionConfidence, "kkbot_detection_confidence", confidence);
    public void RecordDetectionLatency(double ms) => TrackAndRecord(DetectionLatencyMs, "kkbot_detection_latency_ms", ms);
    public void IncrementOcrRequests() => TrackAndIncrement(OcrRequestsTotal, "kkbot_ocr_requests_total", 1);
    public void IncrementOcrRequestsFailed() => TrackAndIncrement(OcrRequestsFailed, "kkbot_ocr_requests_failed_total", 1);
    public void RecordOcrLatency(double ms) => TrackAndRecord(OcrLatencyMs, "kkbot_ocr_latency_ms", ms);
    public void RecordOcrConfidence(double confidence) => TrackAndRecord(OcrConfidence, "kkbot_ocr_confidence", confidence);

    public void IncrementAiInferences() => TrackAndIncrement(AiInferencesTotal, "kkbot_ai_inferences_total", 1);
    public void IncrementAiInferencesFailed() => TrackAndIncrement(AiInferencesFailed, "kkbot_ai_inferences_failed_total", 1);
    public void RecordAiInferenceLatency(double ms) => TrackAndRecord(AiInferenceLatencyMs, "kkbot_ai_inference_latency_ms", ms);
    public void RecordAiTokensPerRequest(double tokens) => TrackAndRecord(AiTokensPerRequest, "kkbot_ai_tokens_per_request", tokens);
    public void RecordAiTokensPerSecond(double tokensPerSec) => TrackAndRecord(AiTokensPerSecond, "kkbot_ai_tokens_per_second", tokensPerSec);
    public void IncrementAiStreamingChunks() => TrackAndIncrement(AiStreamingChunks, "kkbot_ai_streaming_chunks_total", 1);

    public void IncrementPluginLoads() => TrackAndIncrement(PluginLoadsTotal, "kkbot_plugin_loads_total", 1);
    public void IncrementPluginLoadsFailed() => TrackAndIncrement(PluginLoadsFailed, "kkbot_plugin_loads_failed_total", 1);
    public void RecordPluginLoadDuration(double ms) => TrackAndRecord(PluginLoadDurationMs, "kkbot_plugin_load_duration_ms", ms);
    public void SetPluginsLoaded(long value) => TrackAndSet(PluginsLoaded, "kkbot_plugins_loaded", value);
    public void IncrementPluginExecutions() => TrackAndIncrement(PluginExecutionsTotal, "kkbot_plugin_executions_total", 1);
    public void IncrementPluginExecutionsFailed() => TrackAndIncrement(PluginExecutionsFailed, "kkbot_plugin_executions_failed_total", 1);
    public void RecordPluginExecutionLatency(double ms) => TrackAndRecord(PluginExecutionLatencyMs, "kkbot_plugin_execution_latency_ms", ms);

    public void IncrementStateTransitions() => TrackAndIncrement(StateTransitionsTotal, "kkbot_state_transitions_total", 1);
    public void IncrementRecoveryAttempts() => TrackAndIncrement(RecoveryAttemptsTotal, "kkbot_recovery_attempts_total", 1);
    public void IncrementRecoverySuccesses() => TrackAndIncrement(RecoverySuccesses, "kkbot_recovery_successes_total", 1);
    public void IncrementRecoveryFailures() => TrackAndIncrement(RecoveryFailures, "kkbot_recovery_failures_total", 1);
    public void RecordRecoveryDuration(double ms) => TrackAndRecord(RecoveryDurationMs, "kkbot_recovery_duration_ms", ms);
    public void RecordDagExecutionDuration(double ms) => TrackAndRecord(DagExecutionDurationMs, "kkbot_dag_execution_duration_ms", ms);
    public void IncrementSagaExecutions() => TrackAndIncrement(SagaExecutionsTotal, "kkbot_saga_executions_total", 1);
    public void IncrementSagaCompensations() => TrackAndIncrement(SagaCompensationsTotal, "kkbot_saga_compensations_total", 1);

    public void IncrementMarketplaceDownloads() => TrackAndIncrement(MarketplaceDownloadsTotal, "kkbot_marketplace_downloads_total", 1);
    public void IncrementMarketplaceInstalls() => TrackAndIncrement(MarketplaceInstallsTotal, "kkbot_marketplace_installs_total", 1);
    public void IncrementMarketplacePublishes() => TrackAndIncrement(MarketplacePublishesTotal, "kkbot_marketplace_publishes_total", 1);
    public void IncrementMarketplaceSecurityScans() => TrackAndIncrement(MarketplaceSecurityScansTotal, "kkbot_marketplace_security_scans_total", 1);
    public void IncrementMarketplaceSecurityIssuesFound() => TrackAndIncrement(MarketplaceSecurityIssuesFound, "kkbot_marketplace_security_issues_found_total", 1);
    public void RecordMarketplacePluginRating(double rating) => TrackAndRecord(MarketplacePluginRating, "kkbot_marketplace_plugin_rating", rating);

    public void SetActiveSessions(long value) => TrackAndSet(ActiveSessions, "kkbot_active_sessions", value);
    public void SetConnectedUsers(long value) => TrackAndSet(ConnectedUsers, "kkbot_connected_users", value);
    public void IncrementMessagesSent() => TrackAndIncrement(MessagesSent, "kkbot_messages_sent_total", 1);
    public void IncrementRecordingsStarted() => TrackAndIncrement(RecordingsStarted, "kkbot_recordings_started_total", 1);
    public void IncrementScreenSharesStarted() => TrackAndIncrement(ScreenSharesStarted, "kkbot_screen_shares_started_total", 1);

    public void IncrementHttpRequests() => TrackAndIncrement(HttpRequestsTotal, "kkbot_http_requests_total", 1);
    public void IncrementHttpRequestsFailed() => TrackAndIncrement(HttpRequestsFailed, "kkbot_http_requests_failed_total", 1);
    public void RecordHttpRequestDuration(double ms) => TrackAndRecord(HttpRequestDurationMs, "kkbot_http_request_duration_ms", ms);
    public void RecordHttpRequestSize(double bytes) => TrackAndRecord(HttpRequestSizeBytes, "kkbot_http_request_size_bytes", bytes);
    public void RecordHttpResponseSize(double bytes) => TrackAndRecord(HttpResponseSizeBytes, "kkbot_http_response_size_bytes", bytes);

    private void TrackAndIncrement(Counter<long> counter, string name, long value)
    {
        counter.Add(value);
        _currentValues.AddOrUpdate(name, value, (_, current) => current + value);
    }

    private void TrackAndSet(UpDownCounter<long> counter, string name, long value)
    {
        var current = _currentValues.GetValueOrDefault(name, 0);
        var delta = value - (long)current;
        if (delta != 0)
        {
            counter.Add(delta);
        }
        _currentValues[name] = value;
    }

    private void TrackAndRecord(Histogram<double> histogram, string name, double value)
    {
        histogram.Record(value);
        _currentValues[name] = value; // For histograms, track last value
    }

    public double? GetCurrentValue(string metricName)
    {
        return _currentValues.TryGetValue(metricName, out var value) ? value : null;
    }

    public IReadOnlyDictionary<string, double> GetAllCurrentValues()
    {
        return _currentValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _meter?.Dispose();
    }
}