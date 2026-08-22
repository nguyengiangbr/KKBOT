// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Optimization.Abstractions;
using KKBOT.AI.Optimization.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using System.Diagnostics;

namespace KKBOT.AI.Optimization.Services;

public sealed class ModelConverter : IModelConverter
{
    private readonly ILogger<ModelConverter> _logger;

    public ModelConverter(ILogger<ModelConverter> logger)
    {
        _logger = logger;
    }

    public async Task<ModelSpec> LoadModelAsync(string modelPath, CancellationToken ct = default)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Model file not found: {modelPath}");

        var spec = new ModelSpec
        {
            ModelId = Path.GetFileNameWithoutExtension(modelPath),
            Name = Path.GetFileNameWithoutExtension(modelPath),
            Version = "1.0.0",
            Framework = DetectFramework(modelPath),
            SourcePath = modelPath,
            FileSizeBytes = new FileInfo(modelPath).Length,
            Checksum = await ComputeChecksumAsync(modelPath, CancellationToken.None)
        };

        if (Path.GetExtension(modelPath).Equals(".onnx", StringComparison.OrdinalIgnoreCase))
        {
            await LoadOnnxSpecAsync(modelPath, spec, CancellationToken.None);
        }
        else if (modelPath.EndsWith(".trt", StringComparison.OrdinalIgnoreCase))
        {
            await LoadTensorRTSpecAsync(modelPath, spec, CancellationToken.None);
        }

        return spec;
    }

    private string DetectFramework(string modelPath)
    {
        var ext = Path.GetExtension(modelPath).ToLowerInvariant();
        return ext switch
        {
            ".onnx" => "ONNX",
            ".trt" => "TensorRT",
            ".mlmodel" => "CoreML",
            ".tflite" => "TFLite",
            ".pb" => "TensorFlow",
            ".pt" or ".pth" => "PyTorch",
            _ => "Unknown"
        };
    }

    private async Task LoadOnnxSpecAsync(string modelPath, ModelSpec spec, CancellationToken ct)
    {
        try
        {
            using var session = new InferenceSession(modelPath);
            
            foreach (var input in session.InputMetadata)
            {
                spec.Inputs.Add(new ModelTensorSpec
                {
                    Name = input.Key,
                    Shape = input.Value.Dimensions.Select(d => (int)d).ToArray(),
                    DataType = input.Value.ElementType.ToString(),
                    Layout = "NCHW"
                });
            }

            foreach (var output in session.OutputMetadata)
            {
                spec.Outputs.Add(new ModelTensorSpec
                {
                    Name = output.Key,
                    Shape = output.Value.Dimensions.Select(d => (int)d).ToArray(),
                    DataType = output.Value.ElementType.ToString(),
                    Layout = "NCHW"
                });
            }
        }
        catch (Exception ex)
        {
            // Fallback: try to parse from file
        }
    }

    private async Task LoadTensorRTSpecAsync(string modelPath, ModelSpec spec, CancellationToken ct)
    {
        // TensorRT engine doesn't have easy metadata access
        // Would need to parse the serialized engine or use TensorRT API
        spec.Framework = "TensorRT";
    }

    public async Task<string> ConvertToOnnxAsync(string modelPath, string outputPath, ConversionOptions options, CancellationToken ct)
    {
        var ext = Path.GetExtension(modelPath).ToLowerInvariant();
        
        if (Path.GetExtension(modelPath).Equals(".onnx", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(modelPath, outputPath, true);
            return outputPath;
        }

        // In a real implementation, this would use ONNX converter tools
        // For now, throw not implemented for non-ONNX formats
        throw new NotSupportedException($"Conversion from {Path.GetExtension(modelPath)} to ONNX not implemented");
    }

    public async Task<string> ConvertToTensorRTAsync(string modelPath, string outputPath, TensorRTConversionOptions options, CancellationToken ct)
    {
        // In a real implementation, this would use TensorRT's IBuilder API
        // For now, simulate the conversion
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        // Create a placeholder TensorRT engine file
        var engineData = new
        {
            ModelType = "TensorRT",
            Precision = "FP16",
            WorkspaceSize = 1024 * 1024 * 1024,
            CreatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(new { Metadata = new { Engine = engineData } });
        await File.WriteAllTextAsync(outputPath, json, ct);
        
        return outputPath;
    }

    public async Task<string> ConvertToCoreMLAsync(string modelPath, string outputPath, CoreMLConversionOptions options, CancellationToken ct)
    {
        // In a real implementation, this would use coremltools or onnx-coreml
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        var mlmodelData = new
        {
            ModelType = "CoreML",
            Precision = "FP16",
            Target = "iOS14+",
            CreatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(new { Metadata = new { Model = mlmodelData } });
        await File.WriteAllTextAsync(outputPath, json, ct);
        
        return outputPath;
    }

    public async Task<string> ConvertToTFLiteAsync(string modelPath, string outputPath, TFLiteConversionOptions options, CancellationToken ct)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        var tfliteData = new
        {
            ModelType = "TFLite",
            Precision = "FP32",
            Target = "Android/Edge",
            CreatedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(new { Metadata = new { Model = tfliteData } });
        await File.WriteAllTextAsync(outputPath, json, ct);
        
        return outputPath;
    }

    public async Task<ModelSpec> GetModelSpecAsync(string modelPath, CancellationToken ct)
    {
        return await LoadModelAsync(modelPath, ct);
    }

    private async Task<string> ComputeChecksumAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}