// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace KKBOT.Marketplace.Server.Services;

public interface ISecurityScanningService
{
    Task<SecurityScanResult> ScanPluginAsync(PluginVersion version, Stream pluginStream, CancellationToken ct = default);
    Task<SecurityScanResult> ScanPluginAsync(string pluginId, string version, CancellationToken ct = default);
    Task<SecurityScanResult> GetLatestScanAsync(string pluginId, string version, CancellationToken ct = default);
    Task<List<SecurityScanResult>> GetScanHistoryAsync(string pluginId, string version, CancellationToken ct = default);
    Task<bool> IsPluginSafeAsync(string pluginId, string version, CancellationToken ct = default);
}

public sealed class SecurityScanResult
{
    public string ScanId { get; set; } = Guid.NewGuid().ToString();
    public string PluginId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public SecurityStatus Status { get; set; } = SecurityStatus.Passed;
    public SecurityRiskLevel RiskLevel { get; set; } = SecurityRiskLevel.None;
    public List<SecurityFinding> Findings { get; set; } = new();
    public List<PermissionAnalysis> PermissionAnalysis { get; set; } = new();
    public List<DependencyVulnerability> DependencyVulnerabilities { get; set; } = new();
    public CodeQualityMetrics CodeQuality { get; set; } = new();
    public BehaviorAnalysis Behavior { get; set; } = new();
    public long ScanDurationMs { get; set; }
    public string ScannerVersion { get; set; } = "1.0.0";
}

public enum SecurityStatus
{
    Passed,
    Warning,
    Failed,
    Error
}

public enum SecurityRiskLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}

public sealed class SecurityFinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RuleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Info;
    public string Category { get; set; } = string.Empty; // static, dynamic, dependency, behavior
    public string FilePath { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public string CodeSnippet { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string CweId { get; set; } = string.Empty;
    public string OwaspCategory { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum Severity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

public sealed class PermissionAnalysis
{
    public string Permission { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDeclared { get; set; }
    public bool IsUsed { get; set; }
    public bool IsSuspicious { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> UsageLocations { get; set; } = new();
}

public sealed class DependencyVulnerability
{
    public string DependencyName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string VulnerabilityId { get; set; } = string.Empty; // CVE-xxxx-xxxx
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public string CvssScore { get; set; } = string.Empty;
    public string FixedVersion { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public bool IsDirectDependency { get; set; }
}

public sealed class CodeQualityMetrics
{
    public int LinesOfCode { get; set; }
    public int NumberOfTypes { get; set; }
    public int NumberOfMethods { get; set; }
    public double CyclomaticComplexity { get; set; }
    public int CouplingBetweenObjects { get; set; }
    public double MaintainabilityIndex { get; set; }
    public int CodeDuplicationPercent { get; set; }
    public int CommentDensity { get; set; }
    public int TechnicalDebtMinutes { get; set; }
}

public sealed class BehaviorAnalysis
{
    public bool AccessesFileSystem { get; set; }
    public bool AccessesNetwork { get; set; }
    public bool AccessesRegistry { get; set; }
    public bool ExecutesCommands { get; set; }
    public bool UsesReflection { get; set; }
    public bool UsesUnsafeCode { get; set; }
    public bool AccessesSensitiveApis { get; set; }
    public List<string> SuspiciousPatterns { get; set; } = new();
    public List<string> CalledApis { get; set; } = new();
    public List<string> ExternalDependencies { get; set; } = new();
}

public sealed class SecurityScanOptions
{
    public bool EnableStaticAnalysis { get; set; } = true;
    public bool EnableDependencyCheck { get; set; } = true;
    public bool EnableBehaviorAnalysis { get; set; } = true;
    public bool EnableCodeQuality { get; set; } = true;
    public bool FailOnCritical { get; set; } = true;
    public bool FailOnHigh { get; set; } = false;
    public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
    public string VulnerabilityDatabaseUrl { get; set; } = "https://api.osv.dev/v1/querybatch";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxFileSizeMb { get; set; } = 100;
}

public sealed class SecurityScanningService : ISecurityScanningService
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly IStorageService _storage;
    private readonly ILogger<SecurityScanningService> _logger;
    private readonly SecurityScanOptions _options;
    private readonly HttpClient _httpClient;

    // Security rules database
    private static readonly List<SecurityRule> SecurityRules = new()
    {
        // Injection vulnerabilities
        new SecurityRule("SEC001", "SQL Injection Risk", "Detects potential SQL injection via string concatenation", 
            Severity.High, "static", "A03:2021", "CWE-89",
            new[] { "SqlCommand", "ExecuteReader", "ExecuteNonQuery", "ExecuteScalar" },
            new[] { "+", "String.Format", "string.Concat", "$" }),

        new SecurityRule("SEC002", "Command Injection Risk", "Detects potential command injection",
            Severity.Critical, "static", "A03:2021", "CWE-78",
            new[] { "Process.Start", "ProcessStartInfo", "cmd.exe", "/c", "/k", "bash", "sh -c" },
            new[] { "Arguments", "StartInfo", "UseShellExecute" }),

        // Insecure deserialization
        new SecurityRule("SEC003", "Insecure Deserialization", "Detects unsafe deserialization patterns",
            Severity.High, "static", "A08:2021", "CWE-502",
            new[] { "BinaryFormatter", "ObjectFormatter", "Deserialize", "SoapFormatter", "LosFormatter", "JsonSerializer.Deserialize" },
            new[] { "TypeNameHandling", "TypeNameAssemblyFormatHandling" }),

        // Hardcoded secrets
        new SecurityRule("SEC004", "Hardcoded Secrets", "Detects hardcoded passwords, API keys, tokens",
            Severity.Critical, "static", "A07:2021", "CWE-798",
            new[] { "password", "apikey", "api_key", "secret", "token", "privatekey", "private_key" },
            new[] { "=", ":", "\"", "'" },
            true), // regex patterns

        // Weak cryptography
        new SecurityRule("SEC005", "Weak Cryptography", "Detects use of weak cryptographic algorithms",
            Severity.High, "static", "A02:2021", "CWE-327",
            new[] { "MD5", "SHA1", "DES", "3DES", "RC4", "RC2", "ECB" },
            new[] { "CryptoServiceProvider", "Create", "ComputeHash" }),

        // Insecure random
        new SecurityRule("SEC006", "Insecure Random", "Detects use of non-cryptographic random",
            Severity.Medium, "static", "A02:2021", "CWE-338",
            new[] { "Random", "new Random()", "Next()", "NextDouble()" },
            new[] { "RandomNumberGenerator", "RNGCryptoServiceProvider" }),

        // Path traversal
        new SecurityRule("SEC007", "Path Traversal", "Detects potential path traversal vulnerabilities",
            Severity.High, "static", "A01:2021", "CWE-22",
            new[] { "Path.Combine", "File.Open", "File.ReadAllText", "File.WriteAllText", "Directory.GetFiles" },
            new[] { "..\\", "../", "..%2f", "..%5c" }),

        // XXE
        new SecurityRule("SEC008", "XXE Vulnerability", "Detects XML External Entity vulnerabilities",
            Severity.High, "static", "A05:2021", "CWE-611",
            new[] { "XmlReader", "XmlDocument", "XDocument", "XPathDocument" },
            new[] { "DtdProcessing", "XmlResolver", "ProhibitDtd" }),

        // Debug code
        new SecurityRule("SEC009", "Debug Code in Production", "Detects debug/test code that should not be in production",
            Severity.Medium, "static", "A09:2021", "CWE-489",
            new[] { "Debug.WriteLine", "Console.WriteLine", "Trace.WriteLine", "#if DEBUG", "Debugger.Break" },
            Array.Empty<string>()),

        // Unsafe reflection
        new SecurityRule("SEC010", "Unsafe Reflection", "Detects potentially unsafe reflection usage",
            Severity.Medium, "static", "A08:2021", "CWE-470",
            new[] { "Assembly.Load", "Assembly.LoadFrom", "Assembly.LoadFile", "Activator.CreateInstance", "MethodInfo.Invoke" },
            new[] { "BindingFlags.NonPublic", "BindingFlags.Private" }),

        // Hardcoded URLs/IPs
        new SecurityRule("SEC011", "Hardcoded Network Endpoints", "Detects hardcoded URLs or IP addresses",
            Severity.Low, "static", "A07:2021", "CWE-798",
            new[] { "http://", "https://", "ftp://" },
            new[] { @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}" },
            true),

        // Certificate validation
        new SecurityRule("SEC012", "Disabled Certificate Validation", "Detects disabled SSL/TLS certificate validation",
            Severity.High, "static", "A02:2021", "CWE-295",
            new[] { "ServerCertificateValidationCallback", "RemoteCertificateValidationCallback", "DangerousAcceptAnyServerCertificateValidator" },
            new[] { "return true", "= true" }),

        // Insecure configuration
        new SecurityRule("SEC013", "Insecure Configuration", "Detects insecure configuration patterns",
            Severity.Medium, "static", "A05:2021", "CWE-16",
            new[] { "UseDeveloperExceptionPage", "UseDatabaseErrorPage", "EnableDetailedErrors" },
            new[] { "true", "Development" }),

        // Sensitive data exposure
        new SecurityRule("SEC014", "Sensitive Data Logging", "Detects logging of sensitive information",
            Severity.Medium, "static", "A01:2021", "CWE-532",
            new[] { "LogInformation", "LogDebug", "LogError", "LogWarning", "Logger.Log" },
            new[] { "password", "token", "secret", "key", "ssn", "credit", "card" }),

        // Unvalidated redirects
        new SecurityRule("SEC015", "Open Redirect", "Detects unvalidated redirect vulnerabilities",
            Severity.Medium, "static", "A01:2021", "CWE-601",
            new[] { "Redirect", "RedirectPermanent", "LocalRedirect" },
            new[] { "Request.Query", "Request.Headers", "url", "returnUrl", "redirectUrl" }),

        // Weak authorization
        new SecurityRule("SEC016", "Missing Authorization", "Detects missing authorization checks",
            Severity.High, "static", "A01:2021", "CWE-285",
            new[] { "[Authorize]", "RequireAuthorization", "AuthorizeAttribute" },
            new[] { "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch" },
            true), // Check absence

        // Cryptographic issues
        new SecurityRule("SEC017", "Insecure Key Storage", "Detects hardcoded cryptographic keys",
            Severity.Critical, "static", "A02:2021", "CWE-321",
            new[] { "privatekey", "private_key", "secretkey", "secret_key", "encryptionkey", "encryption_key" },
            new[] { "=", ":", "\"", "'" },
            true),

        // Information exposure
        new SecurityRule("SEC018", "Stack Trace Exposure", "Detects detailed error messages exposed to users",
            Severity.Medium, "static", "A01:2021", "CWE-209",
            new[] { "StackTrace", "Exception.ToString()", "ex.ToString()", "ex.Message" },
            new[] { "return", "throw", "catch" }),

        // Race conditions
        new SecurityRule("SEC019", "Race Condition", "Detects potential race conditions",
            Severity.Medium, "static", "A08:2021", "CWE-362",
            new[] { "File.Exists", "Directory.Exists", "lock", "Monitor.Enter", "Interlocked" },
            new[] { "File.Create", "File.WriteAllText", "File.Delete" }),

        // Insecure defaults
        new SecurityRule("SEC020", "Insecure Default Permissions", "Detects insecure default file/directory permissions",
            Severity.Medium, "static", "A05:2021", "CWE-276",
            new[] { "File.Create", "Directory.CreateDirectory", "File.SetAttributes" },
            new[] { "FileAttributes.Normal", "FileShare.ReadWrite" })
    };

    public SecurityScanningService(
        MarketplaceDbContext dbContext,
        IStorageService storage,
        ILogger<SecurityScanningService> logger,
        IOptions<SecurityScanOptions> options,
        HttpClient httpClient)
    {
        _dbContext = dbContext;
        _storage = storage;
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<SecurityScanResult> ScanPluginAsync(PluginVersion version, Stream pluginStream, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new SecurityScanResult
        {
            PluginId = version.PluginPackage?.PluginId ?? "",
            Version = version.Version,
            ScannedAt = DateTime.UtcNow
        };

        try
        {
            // Save plugin to temp file for analysis
            var tempPath = Path.GetTempFileName();
            try
            {
                using (var fileStream = File.Create(tempPath))
                {
                    await pluginStream.CopyToAsync(fileStream, ct);
                }

                // Run all scans
                if (_options.EnableStaticAnalysis)
                {
                    var staticFindings = await RunStaticAnalysisAsync(tempPath, ct);
                    result.Findings.AddRange(staticFindings);
                }

                if (_options.EnableDependencyCheck)
                {
                    var depVulns = await CheckDependenciesAsync(tempPath, ct);
                    result.DependencyVulnerabilities.AddRange(depVulns);
                }

                if (_options.EnableBehaviorAnalysis)
                {
                    var behavior = await AnalyzeBehaviorAsync(tempPath, ct);
                    result.Behavior = behavior;
                }

                if (_options.EnableCodeQuality)
                {
                    var quality = await AnalyzeCodeQualityAsync(tempPath, ct);
                    result.CodeQuality = quality;
                }

                // Analyze permissions from manifest
                result.PermissionAnalysis = await AnalyzePermissionsAsync(version, ct);

                // Calculate overall status
                result.Status = CalculateOverallStatus(result);
                result.RiskLevel = CalculateRiskLevel(result);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security scan failed for {PluginId} v{Version}", result.PluginId, result.Version);
            result.Status = SecurityStatus.Error;
            result.RiskLevel = SecurityRiskLevel.Critical;
            result.Findings.Add(new SecurityFinding
            {
                RuleId = "SCAN_ERROR",
                Title = "Scan Error",
                Description = ex.Message,
                Severity = Severity.Critical,
                Category = "scanner"
            });
        }

        stopwatch.Stop();
        result.ScanDurationMs = stopwatch.ElapsedMilliseconds;

        // Save scan result
        await SaveScanResultAsync(result, ct);

        // Determine if plugin should be blocked
        if (result.Status == SecurityStatus.Failed && _options.FailOnCritical)
        {
            _logger.LogWarning("Plugin {PluginId} v{Version} failed security scan", result.PluginId, result.Version);
        }

        return result;
    }

    public async Task<SecurityScanResult> ScanPluginAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var versionEntity = await _dbContext.PluginVersions
            .Include(v => v.PluginPackage)
            .FirstOrDefaultAsync(v => v.PluginPackage!.PluginId == pluginId && v.Version == version, ct);

        if (versionEntity == null)
            throw new InvalidOperationException($"Plugin {pluginId} v{version} not found");

        // Download plugin from storage
        var pluginStream = await _storage.DownloadPluginAsync(pluginId, version, ct);
        return await ScanPluginAsync(versionEntity, pluginStream, ct);
    }

    public async Task<SecurityScanResult> GetLatestScanAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var latest = await _dbContext.SecurityScans
            .Where(s => s.PluginId == pluginId && s.Version == version)
            .OrderByDescending(s => s.ScannedAt)
            .FirstOrDefaultAsync(ct);

        return latest == null ? new SecurityScanResult() : ToScanResult(latest);
    }

    public async Task<List<SecurityScanResult>> GetScanHistoryAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var scans = await _dbContext.SecurityScans
            .Where(s => s.PluginId == pluginId && s.Version == version)
            .OrderByDescending(s => s.ScannedAt)
            .ToListAsync(ct);

        return scans.Select(ToScanResult).ToList();
    }

    private static SecurityScanResult ToScanResult(SecurityScan scan)
    {
        return new SecurityScanResult
        {
            ScanId = scan.Id.ToString(),
            PluginId = scan.PluginId,
            Version = scan.Version,
            ScannedAt = scan.ScannedAt,
            Status = scan.Status,
            RiskLevel = scan.RiskLevel,
            Findings = JsonSerializer.Deserialize<List<SecurityFinding>>(scan.FindingsJson) ?? new(),
            PermissionAnalysis = JsonSerializer.Deserialize<List<PermissionAnalysis>>(scan.PermissionsJson) ?? new(),
            DependencyVulnerabilities = JsonSerializer.Deserialize<List<DependencyVulnerability>>(scan.DependencyVulnerabilitiesJson) ?? new(),
            CodeQuality = JsonSerializer.Deserialize<CodeQualityMetrics>(scan.CodeQualityJson) ?? new(),
            Behavior = JsonSerializer.Deserialize<BehaviorAnalysis>(scan.BehaviorJson) ?? new(),
            ScanDurationMs = scan.ScanDurationMs,
            ScannerVersion = scan.ScannerVersion
        };
    }

    public async Task<bool> IsPluginSafeAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var latestScan = await GetLatestScanAsync(pluginId, version, ct);
        return latestScan.Status == SecurityStatus.Passed || latestScan.Status == SecurityStatus.Warning;
    }

    // Private scanning methods

    private async Task<List<SecurityFinding>> RunStaticAnalysisAsync(string filePath, CancellationToken ct)
    {
        var findings = new List<SecurityFinding>();

        try
        {
            // Load assembly with Mono.Cecil
            var assembly = AssemblyDefinition.ReadAssembly(filePath, new ReaderParameters
            {
                ReadSymbols = true,
                ReadingMode = ReadingMode.Deferred
            });

            // Analyze each module
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (!method.HasBody) continue;

                        // Analyze IL instructions
                        var instructions = method.Body.Instructions;
                        for (int i = 0; i < instructions.Count; i++)
                        {
                            var instr = instructions[i];
                            CheckSecurityRules(findings, module, type, method, instr, i);
                        }

                        // Check method body for suspicious patterns
                        CheckMethodBody(findings, type, method);
                    }
                }
            }

            // Also scan embedded resources and config files
            ScanEmbeddedResources(findings, assembly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Static analysis failed for {FilePath}", filePath);
        }

        return findings;
    }

    private void CheckSecurityRules(
        List<SecurityFinding> findings,
        ModuleDefinition module,
        TypeDefinition type,
        MethodDefinition method,
        Instruction instruction,
        int index)
    {
        foreach (var rule in SecurityRules)
        {
            if (!rule.Keywords.Any(k => MatchesInstruction(instruction, k))) continue;

            // Check context patterns
            if (rule.ContextPatterns.Any())
            {
                var context = GetInstructionContext(instruction, method, index, 5);
                if (!rule.ContextPatterns.Any(p => context.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    continue;
            }

            // Check for negative patterns (absence checks)
            if (rule.IsNegativeCheck && rule.NegativeKeywords != null)
            {
                var methodBody = GetIlAsString(method);
                if (rule.NegativeKeywords.Any(k => methodBody.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    continue; // Found the required pattern, so not a violation
            }

            var finding = new SecurityFinding
            {
                RuleId = rule.Id,
                Title = rule.Title,
                Description = rule.Description,
                Severity = rule.Severity,
                Category = rule.Category,
                FilePath = $"{type.FullName}::{method.Name}",
                LineNumber = instruction.Offset,
                CodeSnippet = GetCodeSnippet(instruction, method),
                Recommendation = GetRecommendation(rule),
                CweId = rule.CweId,
                OwaspCategory = rule.OwaspCategory,
                Metadata = new Dictionary<string, string>
                {
                    ["assembly"] = module.Assembly.Name.Name,
                    ["type"] = type.FullName,
                    ["method"] = method.Name,
                    ["instruction"] = instruction.ToString()
                }
            };

            findings.Add(finding);
        }
    }

    private void CheckMethodBody(List<SecurityFinding> findings, TypeDefinition type, MethodDefinition method)
    {
        // Check for hardcoded secrets in method body
        var bodyText = GetIlAsString(method);

        // Check for hardcoded strings that look like secrets
        var stringLiterals = method.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Ldstr)
            .Select(i => i.Operand as string)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToList();

        foreach (var literal in stringLiterals)
        {
            CheckForSecrets(findings, type, method, literal);
        }

        // Check for P/Invoke declarations
        if (method.HasPInvokeInfo)
        {
            findings.Add(new SecurityFinding
            {
                RuleId = "SEC_P_INVOKE",
                Title = "P/Invoke Usage Detected",
                Description = $"Method {method.Name} uses P/Invoke which can bypass security controls",
                Severity = Severity.Medium,
                Category = "static",
                FilePath = $"{type.FullName}::{method.Name}",
                Recommendation = "Review P/Invoke usage for security implications",
                CweId = "CWE-112",
                OwaspCategory = "A08:2021"
            });
        }

        // Check for obsolete APIs
        if (method.HasCustomAttributes)
        {
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name.Contains("Obsolete"))
                {
                    findings.Add(new SecurityFinding
                    {
                        RuleId = "SEC_OBSOLETE_API",
                        Title = "Obsolete API Usage",
                        Description = $"Method {method.Name} uses obsolete API",
                        Severity = Severity.Low,
                        Category = "static",
                        FilePath = $"{type.FullName}::{method.Name}",
                        Recommendation = "Replace with modern alternative"
                    });
                }
            }
        }
    }

    private void CheckForSecrets(List<SecurityFinding> findings, TypeDefinition type, MethodDefinition method, string literal)
    {
        var lower = literal.ToLowerInvariant();
        
        // Check for patterns that look like secrets
        var secretPatterns = new[]
        {
            (pattern: @"(password|pwd|pass)\s*[=:]\s*\S+", "Password"),
            (pattern: @"(api[_-]?key|apikey)\s*[=:]\s*\S+", "API Key"),
            (pattern: @"(secret|token)\s*[=:]\s*\S+", "Secret/Token"),
            (pattern: @"(private[_-]?key|privatekey)\s*[=:]\s*\S+", "Private Key"),
            (pattern: @"(secret[_-]?key|secretkey)\s*[=:]\s*\S+", "Secret Key"),
            (pattern: @"(access[_-]?token|accesstoken)\s*[=:]\s*\S+", "Access Token"),
            (pattern: @"(bearer|auth)\s*[=:]\s*\S+", "Auth Token"),
            (pattern: @"(connectionstring|connstr)\s*[=:]\s*\S+", "Connection String"),
            (pattern: @"(jdbc:|mongodb://|redis://|postgres://|mysql://)", "Database URL")
        };

        foreach (var (pattern, type_) in secretPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(literal, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                findings.Add(new SecurityFinding
                {
                    RuleId = "SEC004",
                    Title = $"Hardcoded {type_} Detected",
                    Description = $"Potential hardcoded {type_.ToLower()} found in code",
                    Severity = Severity.Critical,
                    Category = "static",
                    FilePath = $"{type.FullName}::{method.Name}",
                    CodeSnippet = literal.Length > 100 ? literal.Substring(0, 100) + "..." : literal,
                    Recommendation = "Move secrets to secure configuration or secret manager",
                    CweId = "CWE-798",
                    OwaspCategory = "A07:2021",
                    Metadata = new Dictionary<string, string>
                    {
                        ["matched_pattern"] = pattern,
                        ["literal_length"] = literal.Length.ToString()
                    }
                });
            }
        }
    }

    private bool MatchesInstruction(Instruction instruction, string keyword)
    {
        var operand = instruction.Operand?.ToString() ?? "";
        var opcode = instruction.OpCode.Name;
        
        return operand.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               opcode.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private string GetInstructionContext(Instruction instruction, MethodDefinition method, int index, int window)
    {
        if (!method.HasBody) return "";

        var instructions = method.Body.Instructions;
        var start = Math.Max(0, index - window);
        var end = Math.Min(instructions.Count, index + window + 1);

        var context = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            context.AppendLine($"{instructions[i].Offset:X4}: {instructions[i]}");
        }
        return context.ToString();
    }

    private string GetCodeSnippet(Instruction instruction, MethodDefinition method)
    {
        if (!method.HasBody) return instruction.ToString();

        var instructions = method.Body.Instructions;
        var index = instructions.IndexOf(instruction);
        var start = Math.Max(0, index - 3);
        var end = Math.Min(instructions.Count, index + 4);

        var snippet = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            var prefix = i == index ? ">> " : "   ";
            snippet.AppendLine($"{prefix}{instructions[i].Offset:X4}: {instructions[i]}");
        }
        return snippet.ToString();
    }

    private static string GetIlAsString(MethodDefinition method)
    {
        if (!method.HasBody) return string.Empty;
        var sb = new StringBuilder();
        foreach (var instr in method.Body.Instructions)
            sb.AppendLine(instr.ToString());
        return sb.ToString();
    }

    private static string GetRecommendation(SecurityRule rule)
    {
        return $"Review the use of '{rule.Title}' and ensure it complies with security best practices ({rule.CweId}).";
    }

    private void ScanEmbeddedResources(List<SecurityFinding> findings, AssemblyDefinition assembly)
    {
        foreach (var resource in assembly.MainModule.Resources)
        {
            if (resource is EmbeddedResource embedded)
            {
                var name = embedded.Name.ToLowerInvariant();
                
                // Check for sensitive files
                if (name.EndsWith(".config") || name.EndsWith(".json") || name.EndsWith(".xml") || name.EndsWith(".yml") || name.EndsWith(".yaml"))
                {
                    try
                    {
                        using var stream = embedded.GetResourceStream();
                        using var reader = new StreamReader(stream);
                        var content = reader.ReadToEnd();
                        
                        // Scan config content for secrets
                        ScanConfigContent(findings, embedded.Name, content);
                    }
                    catch { }
                }
            }
        }
    }

    private void ScanConfigContent(List<SecurityFinding> findings, string resourceName, string content)
    {
        // Check for connection strings, API keys, etc.
        var sensitivePatterns = new[]
        {
            (pattern: @"(connectionstring|connstr)\s*[=:]\s*[""']?[^""'\s]+", "Connection String"),
            (pattern: @"(api[_-]?key|apikey)\s*[=:]\s*[""']?\S+", "API Key"),
            (pattern: @"(secret|token)\s*[=:]\s*[""']?\S+", "Secret/Token"),
            (pattern: @"(password|pwd)\s*[=:]\s*[""']?\S+", "Password")
        };

        foreach (var (pattern, type_) in sensitivePatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (Match? match in matches)
            {
                var value = match!.Value;
                findings.Add(new SecurityFinding
                {
                    RuleId = "SEC_CONFIG_SECRET",
                    Title = $"Hardcoded {type_} in Config",
                    Description = $"Potential {type_.ToLower()} found in embedded resource {resourceName}",
                    Severity = Severity.Critical,
                    Category = "static",
                    FilePath = resourceName,
                    CodeSnippet = value.Length > 100 ? value.Substring(0, 100) + "..." : value,
                    Recommendation = "Move secrets to secure configuration or secret manager",
                    CweId = "CWE-798",
                    OwaspCategory = "A07:2021"
                });
            }
        }
    }

    private async Task<List<DependencyVulnerability>> CheckDependenciesAsync(string filePath, CancellationToken ct)
    {
        var vulnerabilities = new List<DependencyVulnerability>();

        try
        {
            // Extract dependencies from assembly
            var assembly = AssemblyDefinition.ReadAssembly(filePath);
            var references = assembly.MainModule.AssemblyReferences;

            var dependencies = references.Select(r => new
            {
                Name = r.Name,
                Version = r.Version.ToString()
            }).ToList();

            // Query vulnerability database (OSV.dev)
            var query = new
            {
                queries = dependencies.Select(d => new
                {
                    package = new { name = d.Name, ecosystem = "NuGet" },
                    version = d.Version
                }).ToList()
            };

            var json = JsonSerializer.Serialize(query);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_options.VulnerabilityDatabaseUrl, content, ct);
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var osvResponse = JsonSerializer.Deserialize<OsvResponse>(responseJson);

                if (osvResponse?.Results != null)
                {
                    foreach (var result in osvResponse.Results)
                    {
                        foreach (var vuln in result.Vulns ?? new List<OsvVuln>())
                        {
                            var dep = dependencies.FirstOrDefault(d => d.Name.Equals(result.Package!.Name, StringComparison.OrdinalIgnoreCase));
                            vulnerabilities.Add(new DependencyVulnerability
                            {
                                DependencyName = result.Package!.Name,
                                Version = dep?.Version ?? "unknown",
                                VulnerabilityId = vuln.Id,
                                Title = vuln.Summary ?? vuln.Details ?? "Vulnerability found",
                                Description = vuln.Details ?? "",
                                Severity = MapSeverity(vuln.Severity),
                                CvssScore = vuln.Affected?.FirstOrDefault()?.Ranges?.FirstOrDefault()?.Events?.FirstOrDefault(e => e.Type == "FIXED")?.Introduced ?? "",
                                FixedVersion = vuln.Affected?.FirstOrDefault()?.Ranges?.FirstOrDefault()?.Events?.FirstOrDefault(e => e.Type == "FIXED")?.Fixed ?? "",
                                ReferenceUrl = vuln.References?.FirstOrDefault()?.Url ?? "",
                                IsDirectDependency = true
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dependency vulnerability check failed");
        }

        return vulnerabilities;
    }

    private async Task<BehaviorAnalysis> AnalyzeBehaviorAsync(string filePath, CancellationToken ct)
    {
        var behavior = new BehaviorAnalysis();

        try
        {
            var assembly = AssemblyDefinition.ReadAssembly(filePath);

            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (!method.HasBody) continue;

                        // Check for file system access
                        if (method.Body.Instructions.Any(i => IsFileSystemCall(i)))
                            behavior.AccessesFileSystem = true;

                        // Check for network access
                        if (method.Body.Instructions.Any(i => IsNetworkCall(i)))
                            behavior.AccessesNetwork = true;

                        // Check for registry access
                        if (method.Body.Instructions.Any(i => IsRegistryCall(i)))
                            behavior.AccessesRegistry = true;

                        // Check for command execution
                        if (method.Body.Instructions.Any(i => IsCommandExecution(i)))
                            behavior.ExecutesCommands = true;

                        // Check for reflection
                        if (method.Body.Instructions.Any(i => IsReflectionCall(i)))
                            behavior.UsesReflection = true;

                        // Check for unsafe code
                        if (method.Body.Instructions.Any(i => i.OpCode == OpCodes.Conv_U || i.OpCode == OpCodes.Conv_I))
                            behavior.UsesUnsafeCode = true;

                        // Collect called APIs
                        CollectCalledApis(behavior, method);

                        // Check for sensitive API usage
                        CheckSensitiveApis(behavior, method);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Behavior analysis failed");
        }

        return behavior;
    }

    private void CollectCalledApis(BehaviorAnalysis behavior, MethodDefinition method)
    {
        foreach (var instr in method.Body.Instructions)
        {
            if (instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt)
            {
                if (instr.Operand is MethodReference methodRef)
                {
                    var fullName = $"{methodRef.DeclaringType.FullName}::{methodRef.Name}";
                    if (!behavior.CalledApis.Contains(fullName))
                        behavior.CalledApis.Add(fullName);
                }
            }
        }
    }

    private void CheckSensitiveApis(BehaviorAnalysis behavior, MethodDefinition method)
    {
        var sensitiveApis = new[]
        {
            "System.Diagnostics.Process::Start",
            "System.IO.File::Delete",
            "System.IO.File::WriteAllText",
            "System.IO.File::WriteAllBytes",
            "System.IO.Directory::Delete",
            "System.Reflection.Assembly::Load",
            "System.Reflection.Assembly::LoadFrom",
            "System.Reflection.Assembly::LoadFile",
            "System.Runtime.InteropServices.Marshal::Copy",
            "System.Runtime.InteropServices.Marshal::GetDelegateForFunctionPointer",
            "Microsoft.Win32.Registry::SetValue",
            "Microsoft.Win32.RegistryKey::SetValue"
        };

        foreach (var instr in method.Body.Instructions)
        {
            if ((instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt) && instr.Operand is MethodReference mr)
            {
                var fullName = $"{mr.DeclaringType.FullName}::{mr.Name}";
                if (sensitiveApis.Contains(fullName))
                {
                    behavior.AccessesSensitiveApis = true;
                    if (!behavior.SuspiciousPatterns.Contains($"Calls sensitive API: {fullName}"))
                        behavior.SuspiciousPatterns.Add($"Calls sensitive API: {fullName}");
                }
            }
        }
    }

    private async Task<CodeQualityMetrics> AnalyzeCodeQualityAsync(string filePath, CancellationToken ct)
    {
        var metrics = new CodeQualityMetrics();

        try
        {
            var assembly = AssemblyDefinition.ReadAssembly(filePath);

            int totalMethods = 0;
            int totalComplexity = 0;
            int totalTypes = 0;
            int totalLines = 0;

            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    if (type.IsInterface || type.IsAbstract) continue;
                    totalTypes++;

                    foreach (var method in type.Methods)
                    {
                        if (!method.HasBody) continue;
                        totalMethods++;

                        // Calculate cyclomatic complexity
                        var complexity = CalculateCyclomaticComplexity(method);
                        totalComplexity += complexity;

                        // Estimate lines of code
                        totalLines += EstimateLinesOfCode(method);
                    }
                }
            }

            metrics.LinesOfCode = totalLines;
            metrics.NumberOfTypes = totalTypes;
            metrics.NumberOfMethods = totalMethods;
            metrics.CyclomaticComplexity = totalMethods > 0 ? (double)totalComplexity / totalMethods : 0;
            
            // Estimate coupling
            metrics.CouplingBetweenObjects = EstimateCoupling(assembly);
            
            // Maintainability index (simplified)
            metrics.MaintainabilityIndex = Math.Max(0, 171 - 5.2 * Math.Log(metrics.LinesOfCode) - 0.23 * metrics.CyclomaticComplexity - 16.2 * Math.Log(metrics.CouplingBetweenObjects + 1));
            
            // Technical debt estimation
            metrics.TechnicalDebtMinutes = (int)(metrics.LinesOfCode * 0.1 + metrics.CyclomaticComplexity * 5);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Code quality analysis failed");
        }

        return metrics;
    }

    private int CalculateCyclomaticComplexity(MethodDefinition method)
    {
        if (!method.HasBody) return 1;

        int complexity = 1; // Base complexity

        foreach (var instr in method.Body.Instructions)
        {
            switch (instr.OpCode.Code)
            {
                case Code.Br:
                case Code.Br_S:
                case Code.Brfalse:
                case Code.Brfalse_S:
                case Code.Brtrue:
                case Code.Brtrue_S:
                case Code.Beq:
                case Code.Beq_S:
                case Code.Bge:
                case Code.Bge_S:
                case Code.Bgt:
                case Code.Bgt_S:
                case Code.Ble:
                case Code.Ble_S:
                case Code.Blt:
                case Code.Blt_S:
                case Code.Bne_Un:
                case Code.Bne_Un_S:
                case Code.Switch:
                    complexity++;
                    break;
            }
        }

        return complexity;
    }

    private int EstimateLinesOfCode(MethodDefinition method)
    {
        if (!method.HasBody) return 0;
        return method.Body.Instructions.Count / 3; // Rough estimate
    }

    private int EstimateCoupling(AssemblyDefinition assembly)
    {
        var externalRefs = new HashSet<string>();
        
        foreach (var module in assembly.Modules)
        {
            foreach (var type in module.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;
                    
                    foreach (var instr in method.Body.Instructions)
                    {
                        if ((instr.OpCode == OpCodes.Call || instr.OpCode == OpCodes.Callvirt) && 
                            instr.Operand is MethodReference mr)
                        {
                            var declaringAssembly = mr.DeclaringType.Scope.Name;
                            if (declaringAssembly != assembly.Name.Name)
                            {
                                externalRefs.Add(declaringAssembly);
                            }
                        }
                    }
                }
            }
        }

        return externalRefs.Count;
    }

    private async Task<List<PermissionAnalysis>> AnalyzePermissionsAsync(PluginVersion version, CancellationToken ct)
    {
        var permissions = new List<PermissionAnalysis>();

        if (version.PluginPackage == null) return permissions;

        var manifestJson = version.ManifestJson;
        if (string.IsNullOrEmpty(manifestJson)) return permissions;

        try
        {
            var manifest = JsonDocument.Parse(manifestJson);
            if (manifest.RootElement.TryGetProperty("RequiredPermissions", out var permsElement))
            {
                foreach (var perm in permsElement.EnumerateArray())
                {
                    var permStr = perm.GetString() ?? "";
                    permissions.Add(new PermissionAnalysis
                    {
                        Permission = permStr,
                        Description = GetPermissionDescription(permStr),
                        IsDeclared = true,
                        IsUsed = await CheckPermissionUsageAsync(permStr, version, CancellationToken.None),
                        IsSuspicious = IsSuspiciousPermission(permStr),
                        RiskLevel = GetPermissionRiskLevel(permStr)
                    });
                }
            }
        }
        catch { }

        return permissions;
    }

    private string GetPermissionDescription(string permission)
    {
        return permission switch
        {
            "DEVICE_READ" => "Read device information and state",
            "INPUT_WRITE" => "Send input commands to device",
            "SCREENSHOT_CAPTURE" => "Capture screen content",
            "OCR_ACCESS" => "Perform OCR on screen content",
            "FILE_READ" => "Read files from device storage",
            "FILE_WRITE" => "Write files to device storage",
            "NETWORK_ACCESS" => "Access network resources",
            "CAMERA_ACCESS" => "Access device camera",
            "MICROPHONE_ACCESS" => "Access device microphone",
            "LOCATION_ACCESS" => "Access device location",
            "CONTACTS_ACCESS" => "Access device contacts",
            "SMS_ACCESS" => "Read/send SMS messages",
            "CALL_LOG_ACCESS" => "Access call logs",
            "CALENDAR_ACCESS" => "Access calendar events",
            _ => "Custom permission"
        };
    }

    private async Task<bool> CheckPermissionUsageAsync(string permission, PluginVersion version, CancellationToken ct)
    {
        // Would analyze code to see if permission is actually used
        // For now, return true for declared permissions
        return true;
    }

    private bool IsSuspiciousPermission(string permission)
    {
        var suspicious = new[] { "SMS_ACCESS", "CALL_LOG_ACCESS", "CONTACTS_ACCESS", "LOCATION_ACCESS", "MICROPHONE_ACCESS", "CAMERA_ACCESS" };
        return suspicious.Contains(permission);
    }

    private string GetPermissionRiskLevel(string permission)
    {
        var highRisk = new[] { "SMS_ACCESS", "CALL_LOG_ACCESS", "CONTACTS_ACCESS", "FILE_WRITE", "MICROPHONE_ACCESS", "CAMERA_ACCESS" };
        var mediumRisk = new[] { "LOCATION_ACCESS", "CONTACTS_ACCESS", "CALENDAR_ACCESS", "NETWORK_ACCESS" };
        
        if (highRisk.Contains(permission)) return "High";
        if (mediumRisk.Contains(permission)) return "Medium";
        return "Low";
    }

    private SecurityStatus CalculateOverallStatus(SecurityScanResult result)
    {
        if (result.Findings.Any(f => f.Severity == Severity.Critical))
            return SecurityStatus.Failed;
        
        if (result.Findings.Any(f => f.Severity == Severity.High))
            return _options.FailOnHigh ? SecurityStatus.Failed : SecurityStatus.Warning;
        
        if (result.Findings.Any(f => f.Severity == Severity.Medium))
            return SecurityStatus.Warning;
        
        if (result.DependencyVulnerabilities.Any(v => v.Severity >= Severity.High))
            return SecurityStatus.Warning;

        return SecurityStatus.Passed;
    }

    private SecurityRiskLevel CalculateRiskLevel(SecurityScanResult result)
    {
        if (result.Findings.Any(f => f.Severity == Severity.Critical))
            return SecurityRiskLevel.Critical;
        
        if (result.Findings.Any(f => f.Severity == Severity.High) || 
            result.DependencyVulnerabilities.Any(v => v.Severity >= Severity.High))
            return SecurityRiskLevel.High;
        
        if (result.Findings.Any(f => f.Severity == Severity.Medium) ||
            result.DependencyVulnerabilities.Any(v => v.Severity == Severity.Medium))
            return SecurityRiskLevel.Medium;
        
        if (result.Findings.Any(f => f.Severity == Severity.Low) ||
            result.DependencyVulnerabilities.Any(v => v.Severity == Severity.Low))
            return SecurityRiskLevel.Low;

        return SecurityRiskLevel.None;
    }

    private async Task SaveScanResultAsync(SecurityScanResult result, CancellationToken ct)
    {
        // Would save to database - need SecurityScan entity
        await Task.CompletedTask;
    }

    // Helper methods

    private bool IsFileSystemCall(Instruction instr)
    {
        if (instr.Operand is MethodReference mr)
        {
            var ns = mr.DeclaringType.Namespace;
            return ns != null && (ns.StartsWith("System.IO") || ns.StartsWith("System.IO.Compression"));
        }
        return false;
    }

    private bool IsNetworkCall(Instruction instr)
    {
        if (instr.Operand is MethodReference mr)
        {
            var ns = mr.DeclaringType.Namespace;
            return ns != null && (ns.StartsWith("System.Net") || ns.StartsWith("System.Net.Http") || ns.StartsWith("System.Net.Sockets"));
        }
        return false;
    }

    private bool IsRegistryCall(Instruction instr)
    {
        if (instr.Operand is MethodReference mr)
        {
            var ns = mr.DeclaringType.Namespace;
            return ns != null && ns.StartsWith("Microsoft.Win32");
        }
        return false;
    }

    private bool IsCommandExecution(Instruction instr)
    {
        if (instr.Operand is MethodReference mr)
        {
            return mr.DeclaringType.FullName == "System.Diagnostics.Process" && mr.Name == "Start";
        }
        return false;
    }

    private bool IsReflectionCall(Instruction instr)
    {
        if (instr.Operand is MethodReference mr)
        {
            var ns = mr.DeclaringType.Namespace;
            return ns != null && ns.StartsWith("System.Reflection");
        }
        return false;
    }

    private Severity MapSeverity(IEnumerable<OsvSeverity>? severities)
    {
        if (severities == null) return Severity.Medium;

        foreach (var s in severities)
        {
            if (s.Type == "CVSS_V3")
            {
                if (double.TryParse(s.Score, out var score))
                {
                    if (score >= 9.0) return Severity.Critical;
                    if (score >= 7.0) return Severity.High;
                    if (score >= 4.0) return Severity.Medium;
                    if (score >= 0.1) return Severity.Low;
                }
            }
        }
        return Severity.Medium;
    }

    // OSV API Response Models
    private class OsvResponse
    {
        public List<OsvResult>? Results { get; set; }
    }

    private class OsvResult
    {
        public OsvPackage? Package { get; set; }
        public List<OsvVuln>? Vulns { get; set; }
    }

    private class OsvPackage
    {
        public string Name { get; set; } = string.Empty;
        public string Ecosystem { get; set; } = string.Empty;
    }

    private class OsvVuln
    {
        public string Id { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public List<OsvSeverity>? Severity { get; set; }
        public List<OsvAffected>? Affected { get; set; }
        public List<OsvReference>? References { get; set; }
    }

    private class OsvSeverity
    {
        public string Type { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty;
    }

    private class OsvAffected
    {
        public List<OsvRange>? Ranges { get; set; }
    }

    private class OsvRange
    {
        public List<OsvEvent>? Events { get; set; }
    }

    private class OsvEvent
    {
        public string Type { get; set; } = string.Empty; // INTRODUCED, FIXED
        public string Introduced { get; set; } = string.Empty;
        public string Fixed { get; set; } = string.Empty;
    }

    private class OsvReference
    {
        public string Type { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    // Security Rule Definition
    private sealed class SecurityRule
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public Severity Severity { get; }
        public string Category { get; }
        public string OwaspCategory { get; }
        public string CweId { get; }
        public string[] Keywords { get; }
        public string[] ContextPatterns { get; }
        public bool IsNegativeCheck { get; }
        public string[]? NegativeKeywords { get; }
        public bool IsRegex { get; }

        public SecurityRule(string id, string title, string description, Severity severity, string category, 
            string owaspCategory, string cweId, string[] keywords, string[] contextPatterns, 
            bool isRegex = false, string[]? negativeKeywords = null, bool isNegativeCheck = false)
        {
            Id = id;
            Title = title;
            Description = description;
            Severity = severity;
            Category = category;
            OwaspCategory = owaspCategory;
            CweId = cweId;
            Keywords = keywords;
            ContextPatterns = contextPatterns;
            IsRegex = isRegex;
            NegativeKeywords = negativeKeywords;
            IsNegativeCheck = isNegativeCheck;
        }
    }
}

// OSV Response models
public class OsvResponse
{
    public List<OsvResult>? Results { get; set; }
}

public class OsvResult
{
    public OsvPackage? Package { get; set; }
    public List<OsvVuln>? Vulns { get; set; }
}

public class OsvPackage
{
    public string Name { get; set; } = string.Empty;
    public string Ecosystem { get; set; } = string.Empty;
}

public class OsvVuln
{
    public string Id { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public List<OsvSeverity>? Severity { get; set; }
    public List<OsvAffected>? Affected { get; set; }
    public List<OsvReference>? References { get; set; }
}

public class OsvSeverity
{
    public string Type { get; set; } = string.Empty;
    public string Score { get; set; } = string.Empty;
}

public class OsvAffected
{
    public List<OsvRange>? Ranges { get; set; }
}

public class OsvRange
{
    public List<OsvEvent>? Events { get; set; }
}

public class OsvEvent
{
    public string Type { get; set; } = string.Empty;
    public string Introduced { get; set; } = string.Empty;
    public string Fixed { get; set; } = string.Empty;
}

public class OsvReference
{
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}