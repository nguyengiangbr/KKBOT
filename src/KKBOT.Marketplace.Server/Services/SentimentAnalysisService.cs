// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace KKBOT.Marketplace.Server.Services;

public interface ISentimentAnalysisService
{
    Task<SentimentResult> AnalyzeAsync(string text, CancellationToken ct = default);
    Task<BatchSentimentResult> AnalyzeBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
    Task<ReviewSentimentSummary> GetPluginSentimentSummaryAsync(string pluginId, CancellationToken ct = default);
    Task<SentimentTrends> GetSentimentTrendsAsync(string pluginId, TimeSpan period, CancellationToken ct = default);
    Task TrainModelAsync(CancellationToken ct = default);
    Task<ModelMetrics> EvaluateModelAsync(CancellationToken ct = default);
}

public sealed class SentimentResult
{
    public string Text { get; set; } = string.Empty;
    public SentimentLabel Label { get; set; }
    public double Score { get; set; } // -1 to 1 (negative to positive)
    public double Confidence { get; set; } // 0 to 1
    public Dictionary<string, double> AspectScores { get; set; } = new(); // performance, stability, ui, support, etc.
    public string[] KeyPhrases { get; set; } = Array.Empty<string>();
    public string[] Entities { get; set; } = Array.Empty<string>();
}

public sealed class BatchSentimentResult
{
    public IReadOnlyList<SentimentResult> Results { get; set; } = Array.Empty<SentimentResult>();
    public SentimentSummary Summary { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }
}

public sealed class SentimentSummary
{
    public int TotalAnalyzed { get; set; }
    public int Positive { get; set; }
    public int Negative { get; set; }
    public int Neutral { get; set; }
    public double AverageScore { get; set; }
    public Dictionary<string, int> AspectDistribution { get; set; } = new();
}

public enum SentimentLabel
{
    Positive,
    Negative,
    Neutral
}

public sealed class ReviewSentimentSummary
{
    public string PluginId { get; set; } = string.Empty;
    public int TotalReviews { get; set; }
    public int AnalyzedReviews { get; set; }
    public SentimentDistribution Distribution { get; set; } = new();
    public double AverageSentimentScore { get; set; }
    public AspectSentiment[] AspectSentiments { get; set; } = Array.Empty<AspectSentiment>();
    public string[] TopPositivePhrases { get; set; } = Array.Empty<string>();
    public string[] TopNegativePhrases { get; set; } = Array.Empty<string>();
    public string[] CommonIssues { get; set; } = Array.Empty<string>();
    public string[] PraisedFeatures { get; set; } = Array.Empty<string>();
}

public sealed class SentimentDistribution
{
    public int Positive { get; set; }
    public int Negative { get; set; }
    public int Neutral { get; set; }
    public double PositivePercentage => Total > 0 ? (double)Positive / Total * 100 : 0;
    public double NegativePercentage => Total > 0 ? (double)Negative / Total * 100 : 0;
    public double NeutralPercentage => Total > 0 ? (double)Neutral / Total * 100 : 0;
    public int Total => Positive + Negative + Neutral;
}

public sealed class AspectSentiment
{
    public string Aspect { get; set; } = string.Empty; // performance, stability, ui, documentation, support, features
    public double AverageScore { get; set; } // -1 to 1
    public int Mentions { get; set; }
    public int PositiveMentions { get; set; }
    public int NegativeMentions { get; set; }
    public double SentimentScore => Mentions > 0 ? (double)(PositiveMentions - NegativeMentions) / Mentions : 0;
}

public sealed class SentimentTrends
{
    public string PluginId { get; set; } = string.Empty;
    public List<SentimentTrendPoint> DailyTrends { get; set; } = new();
    public List<SentimentTrendPoint> WeeklyTrends { get; set; } = new();
    public List<SentimentTrendPoint> MonthlyTrends { get; set; } = new();
    public AspectTrend[] AspectTrends { get; set; } = Array.Empty<AspectTrend>();
    public SentimentShift[] SignificantShifts { get; set; } = Array.Empty<SentimentShift>();
}

public sealed class SentimentTrendPoint
{
    public DateTime Date { get; set; }
    public double AverageScore { get; set; }
    public int ReviewCount { get; set; }
    public int PositiveCount { get; set; }
    public int NegativeCount { get; set; }
    public int NeutralCount { get; set; }
}

public sealed class AspectTrend
{
    public string Aspect { get; set; } = string.Empty;
    public List<TrendDataPoint> ScoreTrend { get; set; } = new();
    public List<TrendDataPoint> MentionTrend { get; set; } = new();
}

public sealed class SentimentShift
{
    public DateTime Date { get; set; }
    public double ScoreChange { get; set; }
    public string Trigger { get; set; } = string.Empty; // version release, update, bug, feature
    public string Description { get; set; } = string.Empty;
}

// ML.NET Data Models
public class SentimentData
{
    [LoadColumn(0)]
    public string Text { get; set; } = string.Empty;

    [LoadColumn(1), ColumnName("Label")]
    public bool Label { get; set; } // true = positive, false = negative
}

public class SentimentPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }
}

public class AspectSentimentData
{
    [LoadColumn(0)]
    public string Text { get; set; } = string.Empty;

    [LoadColumn(1)]
    public string Aspect { get; set; } = string.Empty;

    [LoadColumn(2), ColumnName("Label")]
    public float Label { get; set; } // -1 to 1
}

public sealed class SentimentAnalysisService : ISentimentAnalysisService
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly ILogger<SentimentAnalysisService> _logger;
    private readonly MLContext _mlContext;
    private ITransformer? _sentimentModel;
    private ITransformer? _aspectModel;
    private readonly string _modelPath;

    // Aspect keywords for rule-based fallback
    private static readonly Dictionary<string, string[]> AspectKeywords = new()
    {
        ["performance"] = new[] { "performance", "speed", "fast", "slow", "lag", "fps", "frame rate", "optimization", "cpu", "memory", "resource" },
        ["stability"] = new[] { "crash", "stable", "unstable", "bug", "error", "exception", "freeze", "hang", "reliable", "reliability" },
        ["ui"] = new[] { "ui", "interface", "ui/ux", "design", "layout", "visual", "theme", "dark mode", "responsive", "intuitive" },
        ["documentation"] = new[] { "documentation", "docs", "guide", "tutorial", "readme", "wiki", "example", "documented" },
        ["support"] = new[] { "support", "help", "response", "responsive", "customer service", "ticket", "issue", "fix" },
        ["features"] = new[] { "feature", "functionality", "option", "setting", "customize", "config", "plugin", "extension" },
        ["compatibility"] = new[] { "compatible", "compatibility", "version", "update", "upgrade", "downgrade", "backward" },
        ["security"] = new[] { "security", "secure", "safe", "vulnerability", "exploit", "permission", "privacy" }
    };

    public SentimentAnalysisService(
        MarketplaceDbContext dbContext,
        ILogger<SentimentAnalysisService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _mlContext = new MLContext(seed: 42);
        _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "sentiment");
    }

    public async Task<SentimentResult> AnalyzeAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SentimentResult
            {
                Text = text,
                Label = SentimentLabel.Neutral,
                Score = 0,
                Confidence = 0
            };
        }

        // Try ML model first
        if (_sentimentModel != null)
        {
            try
            {
                var predictionEngine = _mlContext.Model.CreatePredictionEngine<SentimentData, SentimentPrediction>(_sentimentModel);
                var prediction = predictionEngine.Predict(new SentimentData { Text = text });

                var label = prediction.PredictedLabel ? SentimentLabel.Positive : SentimentLabel.Negative;
                if (prediction.Probability < 0.6f) label = SentimentLabel.Neutral;

                var score = prediction.PredictedLabel ? prediction.Score : -prediction.Score;

                return new SentimentResult
                {
                    Text = text,
                    Label = label,
                    Score = score,
                    Confidence = prediction.Probability,
                    AspectScores = await AnalyzeAspectsAsync(text, ct),
                    KeyPhrases = ExtractKeyPhrases(text),
                    Entities = ExtractEntities(text)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML prediction failed, falling back to rule-based");
            }
        }

        // Fallback to rule-based analysis
        return RuleBasedAnalysis(text);
    }

    public async Task<BatchSentimentResult> AnalyzeBatchAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var textsList = texts.ToList();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<SentimentResult>();

        foreach (var text in textsList)
        {
            ct.ThrowIfCancellationRequested();
            var result = await AnalyzeAsync(text, ct);
            results.Add(result);
        }

        stopwatch.Stop();

        var summary = new SentimentSummary
        {
            TotalAnalyzed = results.Count,
            Positive = results.Count(r => r.Label == SentimentLabel.Positive),
            Negative = results.Count(r => r.Label == SentimentLabel.Negative),
            Neutral = results.Count(r => r.Label == SentimentLabel.Neutral),
            AverageScore = results.Average(r => r.Score),
            AspectDistribution = results
                .SelectMany(r => r.AspectScores)
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return new BatchSentimentResult
        {
            Results = results,
            Summary = summary,
            ProcessingTime = stopwatch.Elapsed
        };
    }

    public async Task<ReviewSentimentSummary> GetPluginSentimentSummaryAsync(string pluginId, CancellationToken ct = default)
    {
        var reviews = await _dbContext.PluginReviews
            .Where(r => r.PluginPackage.PluginId == pluginId && !r.IsFlagged)
            .Select(r => new { r.Content, r.Rating, r.CreatedAt })
            .ToListAsync(ct);

        if (!reviews.Any())
        {
            return new ReviewSentimentSummary
            {
                PluginId = pluginId,
                TotalReviews = 0,
                AnalyzedReviews = 0
            };
        }

        var contents = reviews.Select(r => r.Content).ToList();
        var batchResult = await AnalyzeBatchAsync(contents, ct);

        var distribution = new SentimentDistribution
        {
            Positive = batchResult.Summary.Positive,
            Negative = batchResult.Summary.Negative,
            Neutral = batchResult.Summary.Neutral
        };

        // Analyze aspects
        var aspectSentiments = await AnalyzeAspectsForPluginAsync(contents, ct);

        // Extract key phrases
        var allPhrases = batchResult.Results.SelectMany(r => r.KeyPhrases).ToList();
        var positivePhrases = batchResult.Results
            .Where(r => r.Label == SentimentLabel.Positive)
            .SelectMany(r => r.KeyPhrases)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();

        var negativePhrases = batchResult.Results
            .Where(r => r.Label == SentimentLabel.Negative)
            .SelectMany(r => r.KeyPhrases)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();

        // Extract common issues and praised features
        var issues = ExtractIssues(batchResult.Results);
        var praised = ExtractPraisedFeatures(batchResult.Results);

        return new ReviewSentimentSummary
        {
            PluginId = "", // Would be set by caller
            TotalReviews = 0, // Would be set by caller
            AnalyzedReviews = reviews.Count,
            Distribution = new SentimentDistribution
            {
                Positive = batchResult.Summary.Positive,
                Negative = batchResult.Summary.Negative,
                Neutral = batchResult.Summary.Neutral
            },
            AverageSentimentScore = batchResult.Summary.AverageScore,
            AspectSentiments = await AnalyzeAspectsForPluginAsync(contents, ct),
            TopPositivePhrases = positivePhrases,
            TopNegativePhrases = negativePhrases,
            CommonIssues = issues,
            PraisedFeatures = praised
        };
    }

    public async Task<SentimentTrends> GetSentimentTrendsAsync(string pluginId, TimeSpan period, CancellationToken ct = default)
    {
        var fromDate = DateTime.UtcNow.Subtract(period);
        
        var reviews = await _dbContext.PluginReviews
            .Where(r => r.PluginPackage.PluginId == pluginId && r.CreatedAt >= DateTime.UtcNow.Subtract(period) && !r.IsFlagged)
            .Select(r => new { r.Content, r.CreatedAt, r.Rating })
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var dailyTrends = new List<SentimentTrendPoint>();
        var weeklyTrends = new List<SentimentTrendPoint>();
        var monthlyTrends = new List<SentimentTrendPoint>();

        // Group by day
        var dailyGroups = reviews.GroupBy(r => r.CreatedAt.Date).OrderBy(g => g.Key);
        foreach (var group in dailyGroups)
        {
            var contents = group.Select(r => r.Content).ToList();
            var batch = await AnalyzeBatchAsync(contents, CancellationToken.None);
            
            dailyTrends.Add(new SentimentTrendPoint
            {
                Date = group.Key,
                AverageScore = batch.Summary.AverageScore,
                ReviewCount = group.Count(),
                PositiveCount = batch.Summary.Positive,
                NegativeCount = batch.Summary.Negative,
                NeutralCount = batch.Summary.Neutral
            });
        }

        // Weekly aggregation
        var weeklyGroups = reviews.GroupBy(r => GetWeekStart(r.CreatedAt)).OrderBy(g => g.Key);
        foreach (var group in weeklyGroups)
        {
            var contents = group.Select(r => r.Content).ToList();
            var batch = await AnalyzeBatchAsync(contents, CancellationToken.None);
            
            weeklyTrends.Add(new SentimentTrendPoint
            {
                Date = group.Key,
                AverageScore = batch.Summary.AverageScore,
                ReviewCount = group.Count(),
                PositiveCount = batch.Summary.Positive,
                NegativeCount = batch.Summary.Negative,
                NeutralCount = batch.Summary.Neutral
            });
        }

        // Monthly aggregation
        var monthlyGroups = reviews.GroupBy(r => new DateTime(r.CreatedAt.Year, r.CreatedAt.Month, 1)).OrderBy(g => g.Key);
        foreach (var group in monthlyGroups)
        {
            var contents = group.Select(r => r.Content).ToList();
            var batch = await AnalyzeBatchAsync(contents, CancellationToken.None);
            
            monthlyTrends.Add(new SentimentTrendPoint
            {
                Date = group.Key,
                AverageScore = batch.Summary.AverageScore,
                ReviewCount = group.Count(),
                PositiveCount = batch.Summary.Positive,
                NegativeCount = batch.Summary.Negative,
                NeutralCount = batch.Summary.Neutral
            });
        }

        // Aspect trends
        var aspectTrends = await CalculateAspectTrendsAsync(pluginId, ct);

        // Detect significant shifts
        var shifts = DetectSignificantShifts(dailyTrends);

        return new SentimentTrends
        {
            PluginId = pluginId,
            DailyTrends = dailyTrends,
            WeeklyTrends = weeklyTrends,
            MonthlyTrends = monthlyTrends,
            AspectTrends = aspectTrends,
            SignificantShifts = shifts
        };
    }

    public async Task TrainModelAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Training sentiment analysis model...");

        // Prepare training data from reviews with ratings
        var trainingData = await _dbContext.PluginReviews
            .Where(r => !r.IsFlagged && !string.IsNullOrWhiteSpace(r.Content))
            .Select(r => new { r.Content, r.Rating })
            .Take(10000) // Limit for training
            .ToListAsync(ct);

        var trainingExamples = trainingData
            .Select(r => new SentimentData
            {
                Text = r.Content,
                Label = r.Rating >= 4 // 4-5 stars = positive, 1-2 = negative, 3 = neutral (excluded)
            })
            .Where(d => d.Label || !d.Label) // Exclude 3-star (neutral)
            .ToList();

        if (trainingExamples.Count < 100)
        {
            _logger.LogWarning("Insufficient training data for sentiment model");
            return;
        }

        // Split data
        var split = _mlContext.Data.TrainTestSplit(
            _mlContext.Data.LoadFromEnumerable(trainingExamples), 
            testFraction: 0.2);

        // Define pipeline
        var pipeline = _mlContext.Transforms.Text.FeaturizeText(
                outputColumnName: "Features", 
                inputColumnName: nameof(SentimentData.Text))
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: "Label",
                featureColumnName: "Features"));

        // Train
        _sentimentModel = pipeline.Fit(split.TrainSet);

        // Evaluate
        var predictions = _sentimentModel.Transform(split.TestSet);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: "Label");

        _logger.LogInformation("Sentiment model trained. Accuracy: {Accuracy:P2}, AUC: {AUC:P2}", 
            metrics.Accuracy, metrics.AreaUnderRocCurve);

        // Save model
        Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
        _mlContext.Model.Save(_sentimentModel, split.TrainSet.Schema, _modelPath);

        _logger.LogInformation("Model saved to {ModelPath}", _modelPath);
    }

    public async Task<ModelMetrics> EvaluateModelAsync(CancellationToken ct = default)
    {
        if (_sentimentModel == null)
        {
            if (File.Exists(_modelPath))
            {
                _sentimentModel = _mlContext.Model.Load(_modelPath, out _);
            }
            else
            {
                return new ModelMetrics { Accuracy = 0 };
            }
        }

        var testData = await _dbContext.PluginReviews
            .Where(r => !r.IsFlagged && !string.IsNullOrWhiteSpace(r.Content))
            .Select(r => new { r.Content, r.Rating })
            .Take(1000)
            .ToListAsync(ct);

        var testExamples = testData
            .Select(r => new SentimentData
            {
                Text = r.Content,
                Label = r.Rating >= 4
            })
            .Where(d => d.Label || !d.Label)
            .ToList();

        if (testExamples.Count < 50)
            return new ModelMetrics { Accuracy = 0 };

        var testDataView = _mlContext.Data.LoadFromEnumerable(testExamples);
        var predictions = _sentimentModel!.Transform(testDataView);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: "Label");

        return new ModelMetrics
        {
            Accuracy = metrics.Accuracy,
            Auc = metrics.AreaUnderRocCurve,
            F1Score = metrics.F1Score,
            Precision = metrics.PositivePrecision,
            Recall = metrics.PositiveRecall,
            LogLoss = metrics.LogLoss,
            LogLossReduction = metrics.LogLossReduction
        };
    }

    // Private helper methods

    private SentimentResult RuleBasedAnalysis(string text)
    {
        var lowerText = text.ToLowerInvariant();
        
        var positiveWords = new[] { "good", "great", "excellent", "awesome", "amazing", "love", "best", "perfect", "works", "working", "great job", "thanks", "thank you", "recommend", "useful", "helpful", "easy", "simple", "fast", "smooth" };
        var negativeWords = new[] { "bad", "terrible", "awful", "horrible", "worst", "hate", "broken", "broken", "bug", "buggy", "crash", "crashes", "error", "fail", "failed", "slow", "lag", "laggy", "useless", "waste", "disappointed", "disappointing", "frustrated", "frustrating", "unusable", "broken", "doesn't work", "doesnt work", "not working", "stopped working" };

        var positiveCount = positiveWords.Count(w => lowerText.Contains(w));
        var negativeCount = negativeWords.Count(w => lowerText.Contains(w));

        var score = (positiveCount - negativeCount) / (double)Math.Max(1, positiveCount + negativeCount);
        var label = score > 0.1 ? SentimentLabel.Positive : score < -0.1 ? SentimentLabel.Negative : SentimentLabel.Neutral;
        var confidence = Math.Min(1.0, (positiveCount + negativeCount) / 5.0);

        return new SentimentResult
        {
            Text = text,
            Label = label,
            Score = Math.Clamp(score, -1, 1),
            Confidence = confidence,
            AspectScores = AnalyzeAspectsRuleBased(text),
            KeyPhrases = ExtractKeyPhrases(text),
            Entities = ExtractEntities(text)
        };
    }

    private async Task<Dictionary<string, double>> AnalyzeAspectsAsync(string text, CancellationToken ct)
    {
        return AnalyzeAspectsRuleBased(text);
    }

    private Dictionary<string, double> AnalyzeAspectsRuleBased(string text)
    {
        var lowerText = text.ToLowerInvariant();
        var scores = new Dictionary<string, double>();

        foreach (var (aspect, keywords) in AspectKeywords)
        {
            var mentions = keywords.Count(k => lowerText.Contains(k));
            if (mentions > 0)
            {
                // Simple heuristic: positive context near keyword = positive sentiment
                var contextScore = CalculateContextSentiment(text, keywords);
                scores[aspect] = Math.Clamp(contextScore, -1, 1);
            }
            else
            {
                scores[aspect] = 0;
            }
        }

        return scores;
    }

    private double CalculateContextSentiment(string text, string[] keywords)
    {
        var lowerText = text.ToLowerInvariant();
        var positiveContext = new[] { "good", "great", "excellent", "fast", "smooth", "stable", "reliable", "excellent", "perfect", "love", "awesome" };
        var negativeContext = new[] { "bad", "slow", "lag", "crash", "bug", "error", "fail", "unstable", "broken", "terrible", "awful", "horrible" };

        var positiveScore = 0;
        var negativeScore = 0;

        foreach (var keyword in keywords)
        {
            var index = lowerText.IndexOf(keyword);
            if (index >= 0)
            {
                // Check surrounding context (50 chars before and after)
                var start = Math.Max(0, index - 50);
                var end = Math.Min(lowerText.Length, index + keyword.Length + 50);
                var context = lowerText.Substring(start, end - start);

                positiveScore += positiveContext.Count(p => context.Contains(p));
                negativeScore += negativeContext.Count(n => context.Contains(n));
            }
        }

        var total = positiveScore + negativeScore;
        return total > 0 ? (double)(positiveScore - negativeScore) / total : 0;
    }

    private async Task<AspectSentiment[]> AnalyzeAspectsForPluginAsync(IEnumerable<string> contents, CancellationToken ct)
    {
        var aspectData = new Dictionary<string, AspectSentimentData>();

        foreach (var content in contents)
        {
            var lowerContent = content.ToLowerInvariant();
            
            foreach (var (aspect, keywords) in AspectKeywords)
            {
                var mentions = keywords.Count(k => lowerContent.Contains(k));
                if (mentions > 0)
                {
                    if (!aspectData.ContainsKey(aspect))
                        aspectData[aspect] = new AspectSentimentData { Aspect = aspect };

                    var data = aspectData[aspect];
                    data.Mentions++;
                    
                    var sentimentScore = CalculateContextSentiment(content, keywords);
                    if (sentimentScore > 0.1) data.PositiveMentions++;
                    else if (sentimentScore < -0.1) data.NegativeMentions++;
                    
                    data.TotalScore += sentimentScore;
                }
            }
        }

        return aspectData.Values
            .Select(d => new AspectSentiment
            {
                Aspect = d.Aspect,
                AverageScore = d.Mentions > 0 ? d.TotalScore / d.Mentions : 0,
                Mentions = d.Mentions,
                PositiveMentions = d.PositiveMentions,
                NegativeMentions = d.NegativeMentions
            })
            .Where(a => a.Mentions > 0)
            .OrderByDescending(a => a.Mentions)
            .ToArray();
    }

    private async Task<AspectTrend[]> CalculateAspectTrendsAsync(string pluginId, CancellationToken ct)
    {
        var reviews = await _dbContext.PluginReviews
            .Where(r => r.PluginPackage.PluginId == pluginId && !r.IsFlagged)
            .Select(r => new { r.Content, r.CreatedAt })
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var aspects = AspectKeywords.Keys.ToList();
        var trends = new List<AspectTrend>();

        foreach (var aspect in aspects)
        {
            var scoreTrend = new List<TrendDataPoint>();
            var mentionTrend = new List<TrendDataPoint>();

            var dailyGroups = reviews.GroupBy(r => r.CreatedAt.Date).OrderBy(g => g.Key);
            foreach (var group in dailyGroups)
            {
                var contents = group.Select(r => r.Content).ToList();
                var totalScore = 0.0;
                var mentions = 0;

                foreach (var content in contents)
                {
                    var score = CalculateContextSentiment(content, AspectKeywords[aspect]);
                    if (keywords.Any(k => content.ToLowerInvariant().Contains(k)))
                    {
                        mentions++;
                        totalScore += CalculateContextSentiment(content, AspectKeywords[aspect]);
                    }
                }

                scoreTrend.Add(new TrendDataPoint
                {
                    Date = group.Key,
                    Value = mentions > 0 ? totalScore / mentions : 0
                });

                mentionTrend.Add(new TrendDataPoint
                {
                    Date = group.Key,
                    Value = mentions
                });
            }

            if (scoreTrend.Any())
            {
                trends.Add(new AspectTrend
                {
                    Aspect = aspect,
                    ScoreTrend = scoreTrend,
                    MentionTrend = mentionTrend
                });
            }
        }

        return trends.ToArray();
    }

    private SentimentShift[] DetectSignificantShifts(List<SentimentTrendPoint> dailyTrends)
    {
        var shifts = new List<SentimentShift>();
        const double threshold = 0.3; // Significant shift threshold

        for (int i = 1; i < dailyTrends.Count; i++)
        {
            var change = dailyTrends[i].AverageScore - dailyTrends[i - 1].AverageScore;
            if (Math.Abs(change) >= threshold)
            {
                shifts.Add(new SentimentShift
                {
                    Date = dailyTrends[i].Date,
                    ScoreChange = change,
                    Trigger = change > 0 ? "positive_surge" : "negative_drop",
                    Description = $"Sentiment score changed by {change:F2} from previous day"
                });
            }
        }

        return shifts.ToArray();
    }

    private SentimentShift[] DetectSignificantShifts(List<SentimentTrendPoint> trends)
    {
        var shifts = new List<SentimentShift>();
        const double threshold = 0.3;

        for (int i = 1; i < trends.Count; i++)
        {
            var change = trends[i].AverageScore - trends[i - 1].AverageScore;
            if (Math.Abs(change) >= threshold)
            {
                shifts.Add(new SentimentShift
                {
                    Date = trends[i].Date,
                    ScoreChange = change,
                    Trigger = change > 0 ? "positive_surge" : "negative_drop",
                    Description = $"Sentiment shifted by {change:F2}"
                });
            }
        }

        return shifts.ToArray();
    }

    private string[] ExtractKeyPhrases(string text)
    {
        // Simple n-gram extraction
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToList();

        var phrases = new List<string>();
        for (int i = 0; i < words.Count - 1; i++)
        {
            phrases.Add($"{words[i]} {words[i + 1]}");
        }
        for (int i = 0; i < words.Count - 2; i++)
        {
            phrases.Add($"{words[i]} {words[i + 1]} {words[i + 2]}");
        }

        return phrases
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();
    }

    private string[] ExtractEntities(string text)
    {
        // Simple entity extraction (capitalized words, version numbers, etc.)
        var entities = new List<string>();
        
        // Version patterns
        var versionMatches = System.Text.RegularExpressions.Regex.Matches(text, @"\b\d+\.\d+(\.\d+)?\b");
        foreach (Match m in versionMatches) entities.Add(m.Value);

        // Capitalized words (potential proper nouns)
        var capitalized = System.Text.RegularExpressions.Regex.Matches(text, @"\b[A-Z][a-z]{2,}\b");
        foreach (Match m in capitalized) entities.Add(m.Value);

        return entities.Distinct().Take(10).ToArray();
    }

    private string[] ExtractIssues(IEnumerable<SentimentResult> negativeResults)
    {
        return negativeResults
            .SelectMany(r => r.KeyPhrases)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();
    }

    private string[] ExtractPraisedFeatures(IEnumerable<SentimentResult> positiveResults)
    {
        return positiveResults
            .SelectMany(r => r.KeyPhrases)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();
    }
}

public sealed class AspectSentimentData
{
    public string Aspect { get; set; } = string.Empty;
    public int Mentions { get; set; }
    public int PositiveMentions { get; set; }
    public int NegativeMentions { get; set; }
    public double TotalScore { get; set; }
}

public sealed class ModelMetrics
{
    public double Accuracy { get; set; }
    public double Auc { get; set; }
    public double F1Score { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double LogLoss { get; set; }
    public double LogLossReduction { get; set; }
}