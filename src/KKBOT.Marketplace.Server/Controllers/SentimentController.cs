// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.Marketplace.Server.Controllers;

[ApiController]
[Route("api/v1/sentiment")]
[Authorize(Policy = "RequirePermission:analytics.view")]
[Produces("application/json")]
[SwaggerTag("Sentiment Analysis")]
public sealed class SentimentController : ControllerBase
{
    private readonly ISentimentAnalysisService _sentiment;
    private readonly ILogger<SentimentController> _logger;

    public SentimentController(ISentimentAnalysisService sentiment, ILogger<SentimentController> logger)
    {
        _sentiment = sentiment;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(SentimentResult), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Analyze sentiment of a single text")]
    public async Task<ActionResult<SentimentResult>> Analyze([FromBody] AnalyzeSentimentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Text is required" });

        var result = await _sentiment.AnalyzeAsync(request.Text, ct);
        return Ok(result);
    }

    [HttpPost("analyze/batch")]
    [ProducesResponseType(typeof(BatchSentimentResult), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Analyze sentiment of multiple texts")]
    public async Task<ActionResult<BatchSentimentResult>> AnalyzeBatch([FromBody] BatchAnalyzeRequest request, CancellationToken ct)
    {
        if (request.Texts == null || !request.Texts.Any())
            return BadRequest(new { error = "At least one text is required" });

        if (request.Texts.Count() > 1000)
            return BadRequest(new { error = "Maximum 1000 texts per batch" });

        var result = await _sentiment.AnalyzeBatchAsync(request.Texts, ct);
        return Ok(result);
    }

    [HttpGet("plugin/{pluginId}/summary")]
    [ProducesResponseType(typeof(ReviewSentimentSummary), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get sentiment summary for a plugin's reviews")]
    public async Task<ActionResult<ReviewSentimentSummary>> GetPluginSummary(string pluginId, CancellationToken ct)
    {
        try
        {
            var summary = await _sentiment.GetPluginSentimentSummaryAsync(pluginId, ct);
            summary.PluginId = pluginId;
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sentiment summary for plugin {PluginId}", pluginId);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("plugin/{pluginId}/trends")]
    [ProducesResponseType(typeof(SentimentTrends), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get sentiment trends for a plugin over time")]
    public async Task<ActionResult<SentimentTrends>> GetTrends(
        string pluginId,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        try
        {
            var trends = await _sentiment.GetSentimentTrendsAsync(pluginId, TimeSpan.FromDays(days), ct);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sentiment trends for plugin {PluginId}", pluginId);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("train")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(ModelMetrics), 200)]
    [SwaggerOperation("Train the sentiment analysis model")]
    public async Task<ActionResult<ModelMetrics>> TrainModel(CancellationToken ct)
    {
        try
        {
            await _sentiment.TrainModelAsync(ct);
            var metrics = await _sentiment.EvaluateModelAsync(ct);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to train sentiment model");
            return StatusCode(500, new { error = "Training failed", details = ex.Message });
        }
    }

    [HttpGet("model/metrics")]
    [ProducesResponseType(typeof(ModelMetrics), 200)]
    [SwaggerOperation("Get current model evaluation metrics")]
    public async Task<ActionResult<ModelMetrics>> GetModelMetrics(CancellationToken ct)
    {
        var metrics = await _sentiment.EvaluateModelAsync(ct);
        return Ok(metrics);
    }
}

public sealed record AnalyzeSentimentRequest(string Text);
public sealed record BatchAnalyzeRequest(IEnumerable<string> Texts);