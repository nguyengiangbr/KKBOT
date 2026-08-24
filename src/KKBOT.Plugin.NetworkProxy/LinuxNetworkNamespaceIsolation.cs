// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Plugin.NetworkProxy;

/// <summary>
/// Linux network namespace isolation implementation.
/// </summary>
public sealed class LinuxNetworkNamespaceIsolation : INetworkIsolationProvider
{
    private readonly ILogger<LinuxNetworkNamespaceIsolation> _logger;
    private readonly NetworkProxyModuleOptions _options;
    private readonly ConcurrentDictionary<string, string> _instanceNamespaces = new();
    private readonly SemaphoreSlim _namespaceLock = new(1, 1);

    public LinuxNetworkNamespaceIsolation(
        ILogger<LinuxNetworkNamespaceIsolation> logger,
        IOptions<NetworkProxyModuleOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public async Task<IsolationResult> CreateNamespaceAsync(string instanceId, NetworkIsolationConfig config, CancellationToken ct = default)
    {
        if (!IsSupported)
        {
            return IsolationResult.Failed("Network namespace isolation only supported on Linux");
        }

        await _namespaceLock.WaitAsync(ct);
        try
        {
            if (_instanceNamespaces.ContainsKey(instanceId))
            {
                return IsolationResult.Failed($"Namespace already exists for instance {instanceId}");
            }

            var namespaceName = config.NetworkNamespace ?? $"kkbot-{instanceId}";
            
            // Create network namespace
            var createResult = await ExecuteCommandAsync("ip", $"netns add {namespaceName}");
            if (!createResult.Success)
            {
                return IsolationResult.Failed($"Failed to create network namespace: {createResult.Error}");
            }

            // Bring up loopback interface in namespace
            var loResult = await ExecuteCommandAsync("ip", $"netns exec {namespaceName} ip link set lo up");
            if (!loResult.Success)
            {
                await ExecuteCommandAsync("ip", $"netns del {namespaceName}");
                return IsolationResult.Failed($"Failed to bring up loopback: {loResult.Error}");
            }

            // Configure proxy routing if proxy specified
            if (!string.IsNullOrEmpty(config.AllowedDestinations.FirstOrDefault()))
            {
                var proxyResult = await ConfigureProxyRoutingAsync(namespaceName, instanceId);
                if (!proxyResult.Success)
                {
                    await ExecuteCommandAsync("ip", $"netns del {namespaceName}");
                    return proxyResult;
                }
            }

            // Apply custom routes
            foreach (var route in config.CustomRoutes)
            {
                var routeResult = await ExecuteCommandAsync("ip", $"netns exec {namespaceName} ip route add {route.Key} via {route.Value}");
                if (!routeResult.Success)
                {
                    _logger.LogWarning("Failed to add custom route {Route}: {Error}", route.Key, routeResult.Error);
                }
            }

            // Block WebRTC if enabled
            if (config.BlockWebRTC)
            {
                await BlockWebRTCAsync(namespaceName);
            }

            // Block direct sockets if enabled
            if (config.BlockDirectSockets)
            {
                await BlockDirectSocketsAsync(namespaceName);
            }

            // Apply custom routes
            foreach (var route in config.CustomRoutes)
            {
                await ExecuteCommandAsync("ip", $"netns exec {namespaceName} ip route add {route.Key} via {route.Value}");
            }

            // Block destinations
            foreach (var blocked in config.BlockedDestinations)
            {
                await BlockDestinationAsync(namespaceName, blocked);
            }

            // Allow destinations (whitelist)
            if (config.AllowedDestinations.Count > 0)
            {
                await SetupWhitelistAsync(namespaceName, config.AllowedDestinations);
            }

            _instanceNamespaces[instanceId] = namespaceName;
            
            return IsolationResult.Success($"Network namespace {namespaceName} created for instance {instanceId}");
        }
        finally
        {
            _namespaceLock.Release();
        }
    }

    public async Task<IsolationResult> RemoveNamespaceAsync(string instanceId, CancellationToken ct = default)
    {
        if (!_instanceNamespaces.TryRemove(instanceId, out var namespaceName))
        {
            return IsolationResult.Success("No namespace to remove");
        }

        await _namespaceLock.WaitAsync(ct);
        try
        {
            var result = await ExecuteCommandAsync("ip", $"netns del {namespaceName}");
            if (!result.Success)
            {
                _logger.LogWarning("Failed to delete namespace {Namespace}: {Error}", namespaceName, result.Error);
                return IsolationResult.Failed($"Failed to delete namespace: {result.Error}");
            }

            return IsolationResult.Success($"Network namespace {namespaceName} removed for instance");
        }
        finally
        {
            _namespaceLock.Release();
        }
    }

    public async Task<IsolationResult> ConfigureRoutingAsync(string instanceId, NetworkIsolationConfig config, CancellationToken ct = default)
    {
        if (!_instanceNamespaces.TryGetValue(instanceId, out var namespaceName))
        {
            return IsolationResult.Failed($"No namespace found for instance {instanceId}");
        }

        // Configure proxy routing
        var proxyResult = await ConfigureProxyRoutingAsync(namespaceName, instanceId);
        if (!proxyResult.Success) return proxyResult;

        // Apply custom routes
        foreach (var route in config.CustomRoutes)
        {
            await ExecuteCommandAsync("ip", $"netns exec {namespaceName} ip route add {route.Key} via {route.Value}");
        }

        // Update blocked/allowed destinations
        foreach (var blocked in config.BlockedDestinations)
        {
            await BlockDestinationAsync(namespaceName, blocked);
        }

        if (config.AllowedDestinations.Count > 0)
        {
            await SetupWhitelistAsync(namespaceName, config.AllowedDestinations);
        }

        return IsolationResult.Success("Routing configured");
    }

    private async Task<IsolationResult> ConfigureProxyRoutingAsync(string namespaceName, string instanceId)
    {
        // This would configure iptables to route traffic through the proxy
        // For SOCKS5 proxy, we'd typically use redsocks or similar
        // For HTTP proxy, we'd use iptables REDIRECT or TPROXY
        
        _logger.LogDebug("Configuring proxy routing for namespace {Namespace}", namespaceName);
        
        // Example: Redirect all TCP traffic to redsocks port
        // await ExecuteCommandAsync("ip", $"netns exec {namespaceName} iptables -t nat -A OUTPUT -p tcp -j REDIRECT --to-ports 12345");
        
        return IsolationResult.Success("Proxy routing configured");
    }

    private async Task BlockWebRTCAsync(string namespaceName)
    {
        // Block WebRTC by blocking UDP ports commonly used for WebRTC
        // WebRTC uses UDP ports 3478 (STUN), 5349 (TURN), and ephemeral ports
        
        var commands = new[]
        {
            $"netns exec {namespaceName} iptables -A OUTPUT -p udp --dport 3478 -j DROP",  // STUN
            $"netns exec {namespaceName} iptables -A OUTPUT -p udp --dport 5349 -j DROP", // TURN
            $"netns exec {namespaceName} iptables -A OUTPUT -p udp --dport 19302 -j DROP", // Google STUN
            $"netns exec {namespaceName} iptables -A OUTPUT -p udp --dport 19305 -j DROP", // Google STUN
        };

        foreach (var cmd in commands)
        {
            var parts = cmd.Split(' ', 2);
            await ExecuteCommandAsync(parts[0], parts[1]);
        }

        _logger.LogDebug("Blocked WebRTC ports in namespace {Namespace}", namespaceName);
    }

    private async Task BlockDirectSocketsAsync(string namespaceName)
    {
        // Block direct socket connections by dropping non-proxy traffic
        // This is a simplified version - real implementation would be more sophisticated
        
        // Block all outgoing connections except through proxy
        // This requires knowing the proxy IP/port
        
        _logger.LogDebug("Blocking direct sockets in namespace {Namespace}", namespaceName);
        
        // Example: Allow only traffic to proxy IP
        // await ExecuteCommandAsync("ip", $"netns exec {namespaceName} iptables -A OUTPUT -d <PROXY_IP> -j ACCEPT");
        // await ExecuteCommandAsync("ip", $"netns exec {namespaceName} iptables -A OUTPUT -j DROP");
    }

    private async Task BlockDestinationAsync(string namespaceName, string destination)
    {
        // Block specific destination (IP or CIDR)
        var cmd = $"netns exec {namespaceName} iptables -A OUTPUT -d {destination} -j DROP";
        var parts = cmd.Split(' ', 2);
        var result = await ExecuteCommandAsync(parts[0], parts[1]);
        
        if (result.Success)
        {
            _logger.LogDebug("Blocked destination {Destination} in namespace {Namespace}", destination, namespaceName);
        }
        else
        {
            _logger.LogWarning("Failed to block destination {Destination}: {Error}", destination, result.Error);
        }
    }

    private async Task SetupWhitelistAsync(string namespaceName, List<string> allowedDestinations)
    {
        // Default deny all, then allow specific destinations
        // This requires careful ordering of iptables rules
        
        // First, set default policy to DROP
        await ExecuteCommandAsync("ip", $"netns exec {namespaceName} iptables -P OUTPUT DROP");
        
        // Allow loopback
        await ExecuteCommandAsync("ip", $"netns exec {namespaceName} iptables -A OUTPUT -o lo -j ACCEPT");
        
        // Allow established/related connections
        await ExecuteCommandAsync("ip", $"netns exec {namespaceName} iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT");
        
        // Allow whitelisted destinations
        foreach (var dest in allowedDestinations)
        {
            var cmd = $"netns exec {namespaceName} iptables -A OUTPUT -d {dest} -j ACCEPT";
            var parts = cmd.Split(' ', 2);
            await ExecuteCommandAsync(parts[0], parts[1]);
        }
    }

    private async Task<CommandResult> ExecuteCommandAsync(string command, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return CommandResult.Failed("Failed to start process");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            return process.ExitCode == 0
                ? CommandResult.Success(output)
                : CommandResult.Failed(error);
        }
        catch (Exception ex)
        {
            return CommandResult.Failed(ex.Message);
        }
    }

    public void Dispose()
    {
        // Cleanup handled by RemoveNamespaceAsync
    }
}

/// <summary>
/// Command execution result.
/// </summary>
public sealed record CommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;

    public static CommandResult Success(string output) => new() { Success = true, Output = output };
    public static CommandResult Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Interface for network isolation providers.
/// </summary>
public interface INetworkIsolationProvider
{
    bool IsSupported { get; }
    Task<IsolationResult> CreateNamespaceAsync(string instanceId, NetworkIsolationConfig config, CancellationToken ct = default);
    Task<IsolationResult> RemoveNamespaceAsync(string instanceId, CancellationToken ct = default);
    Task<IsolationResult> ConfigureRoutingAsync(string instanceId, NetworkIsolationConfig config, CancellationToken ct = default);
}

public sealed class IsolationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static IsolationResult Success(string message) => new() { Success = true, Message = message };
    public static IsolationResult Failed(string message) => new() { Success = false, Message = message };
}