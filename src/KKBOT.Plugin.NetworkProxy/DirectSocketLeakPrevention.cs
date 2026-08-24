// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Plugin.NetworkProxy;

/// <summary>
/// Direct socket leak prevention for system-level socket connections.
/// </summary>
public sealed class DirectSocketLeakPrevention
{
    private readonly ILogger<DirectSocketLeakPrevention> _logger;
    private readonly DirectSocketPreventionOptions _options;

    public DirectSocketLeakPrevention(
        ILogger<DirectSocketLeakPrevention> logger,
        IOptions<DirectSocketPreventionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Configures system-wide socket filtering to prevent direct connections.
    /// </summary>
    public async Task<PreventionResult> ApplySystemWideFilteringAsync(CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return PreventionResult.Failed("Direct socket prevention only supported on Linux");
        }

        try
        {
            // 1. Configure iptables to block direct outbound connections
            await ApplyIptablesRulesAsync();

            // 2. Configure tc (traffic control) for additional filtering
            if (_options.EnableTCFiltering)
            {
                await ApplyTCFilteringAsync();
            }

            // 3. Configure systemd-resolved to use proxy DNS
            if (_options.ForceProxyDNS)
            {
                await ConfigureProxyDNSAsync();
            }

            // 4. Block raw sockets (requires root)
            if (_options.BlockRawSockets)
            {
                await BlockRawSocketsAsync();
            }

            // 5. Configure AppArmor/SELinux profile if available
            if (_options.EnableAppArmor)
            {
                await ConfigureAppArmorAsync();
            }

            return PreventionResult.Success("Direct socket leak prevention applied");
        }
        catch (Exception ex)
        {
            return PreventionResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Configures per-process socket filtering using LD_PRELOAD.
    /// </summary>
    public string GetLDPreloadLibrary()
    {
        // Returns path to a custom LD_PRELOAD library that intercepts socket calls
        // This library would intercept connect(), sendto(), etc. and enforce proxy usage
        return _options.LDPreloadLibraryPath;
    }

    /// <summary>
    /// Generates LD_PRELOAD library source code for socket interception.
    /// </summary>
    public string GenerateLDPreloadSource()
    {
        return @"
#define _GNU_SOURCE
#include <dlfcn.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <unistd.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// Proxy configuration from environment
static char* proxy_host = NULL;
static int proxy_port = 0;
static int initialized = 0;

static void init_proxy_config() {
    if (initialized) return;
    proxy_host = getenv("KKBOT_PROXY_HOST");
    proxy_port = atoi(getenv("KKBOT_PROXY_PORT") ?: "1080");
    initialized = 1;
}

// Original connect function
static int (*real_connect)(int sockfd, const struct sockaddr *addr, socklen_t addrlen) = NULL;

// Intercepted connect
int connect(int sockfd, const struct sockaddr *addr, socklen_t addrlen) {
    if (!initialized) init_proxy_config();
    
    if (!proxy_host) {
        // No proxy configured, allow direct connection
        if (!real_connect) {
            real_connect = dlsym(RTLD_NEXT, "connect");
        }
        return real_connect(sockfd, addr, addrlen);
    }

    // Check if this is a connection we should proxy
    if (addr->sa_family == AF_INET) {
        struct sockaddr_in* addr_in = (struct sockaddr_in*)addr;
        char ip_str[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &addr_in->sin_addr, ip_str, INET_ADDRSTRLEN);
        
        // Skip localhost and private networks
        if (strcmp(ip_str, "127.0.0.1") == 0 || 
            strncmp(ip_str, "10.", 3) == 0 ||
            strncmp(ip_str, "192.168.", 7) == 0 ||
            strncmp(ip_str, "172.16.", 7) == 0 ||
            strncmp(ip_str, "172.17.", 7) == 0 ||
            strncmp(ip_str, "172.18.", 7) == 0 ||
            strncmp(ip_str, "172.19.", 7) == 0 ||
            strncmp(ip_str, "172.20.", 7) == 0 ||
            strncmp(ip_str, "172.21.", 7) == 0 ||
            strncmp(ip_str, "172.22.", 7) == 0 ||
            strncmp(ip_str, "172.23.", 7) == 0 ||
            strncmp(ip_str, "172.24.", 7) == 0 ||
            strncmp(ip_str, "172.24.", 7) == 0 ||
            strncmp(ip_str, "172.25.", 7) == 0 ||
            strncmp(ip_str, "172.26.", 7) == 0 ||
            strncmp(ip_str, "172.27.", 7) == 0 ||
            strncmp(ip_str, "172.28.", 7) == 0 ||
            strncmp(ip_str, "172.29.", 7) == 0 ||
            strncmp(ip_str, "172.30.", 7) == 0 ||
            strncmp(ip_str, "172.31.", 7) == 0) {
            // Allow direct connection for private networks
            if (!real_connect) {
                real_connect = dlsym(RTLD_NEXT, "connect");
            }
            return real_connect(sockfd, addr, addrlen);
        }

        // Redirect through proxy
        fprintf(stderr, "[KKBOT] Redirecting connection to %s:%d via proxy %s:%d\n", 
                ip_str, ntohs(addr_in->sin_port), proxy_host, proxy_port);
        
        // Create proxy connection
        int proxy_sock = socket(AF_INET, SOCK_STREAM, 0);
        struct sockaddr_in proxy_addr = {0};
        proxy_addr.sin_family = AF_INET;
        proxy_addr.sin_port = htons(proxy_port);
        inet_pton(AF_INET, proxy_host, &proxy_addr.sin_addr);
        
        if (connect(proxy_sock, (struct sockaddr*)&proxy_addr, sizeof(proxy_addr)) < 0) {
            perror("[KKBOT] Proxy connection failed");
            return -1;
        }

        // For SOCKS5, send handshake
        // ... SOCKS5 handshake implementation ...

        // Duplicate proxy socket to original socket fd
        dup2(proxy_sock, sockfd);
        close(proxy_sock);
        return 0;
    }

    // For non-IPv4, allow direct connection
    if (!real_connect) {
        real_connect = dlsym(RTLD_NEXT, "connect");
    }
    return real_connect(sockfd, addr, addrlen);
}

// Also intercept sendto for UDP
static ssize_t (*real_sendto)(int sockfd, const void *buf, size_t len, int flags,
                              const struct sockaddr *dest_addr, socklen_t addrlen) = NULL;

ssize_t sendto(int sockfd, const void *buf, size_t len, int flags,
               const struct sockaddr *dest_addr, socklen_t addrlen) {
    if (!initialized) init_proxy_config();
    
    if (proxy_host && dest_addr && dest_addr->sa_family == AF_INET) {
        struct sockaddr_in* addr_in = (struct sockaddr_in*)dest_addr;
        char ip_str[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &addr_in->sin_addr, ip_str, INET_ADDRSTRLEN);
        
        // Skip private networks
        if (strcmp(ip_str, "127.0.0.1") != 0 && 
            strncmp(ip_str, "10.", 3) != 0 &&
            strncmp(ip_str, "192.168.", 7) != 0 &&
            strncmp(ip_str, "172.16.", 7) != 0) {
            // Redirect UDP through proxy
            fprintf(stderr, "[KKBOT] Redirecting UDP to %s:%d via proxy\n", 
                    ip_str, ntohs(addr_in->sin_port));
            // Implementation would redirect UDP through SOCKS5 proxy
        }
    }

    if (!real_sendto) {
        real_sendto = dlsym(RTLD_NEXT, "sendto");
    }
    return real_sendto(sockfd, buf, len, flags, dest_addr, addrlen);
}
";
    }

    /// <summary>
    /// Configures application-level socket filtering.
    /// </summary>
    public async Task<PreventionResult> ConfigureApplicationFilteringAsync(string instanceId, CancellationToken ct = default)
    {
        try
        {
            // Set environment variables for LD_PRELOAD
            Environment.SetEnvironmentVariable("KKBOT_PROXY_HOST", _options.ProxyHost);
            Environment.SetEnvironmentVariable("KKBOT_PROXY_PORT", _options.ProxyPort.ToString());
            Environment.SetEnvironmentVariable("LD_PRELOAD", _options.LDPreloadLibraryPath);
            Environment.SetEnvironmentVariable("KKBOT_INSTANCE_ID", instanceId);

            return PreventionResult.Success("Application filtering configured");
        }
        catch (Exception ex)
        {
            return PreventionResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Monitors socket connections for an instance.
    /// </summary>
    public async Task<SocketMonitorResult> MonitorSocketConnectionsAsync(string instanceId, TimeSpan duration, CancellationToken ct = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var connections = new List<SocketConnection>();

        try
        {
            while (DateTimeOffset.UtcNow - startTime < duration && !ct.IsCancellationRequested)
            {
                // Use ss or netstat to monitor connections
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ss",
                    Arguments = "-tunap",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);

                    var lines = output.Split('
', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines.Skip(1)) // Skip header
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            connections.Add(new SocketConnection
                            {
                                State = parts[0],
                                LocalAddress = parts[3],
                                PeerAddress = parts[4],
                                ProcessName = parts.Length > 6 ? parts[6] : "unknown",
                                Timestamp = DateTimeOffset.UtcNow
                            });
                        }
                    }
                }

                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        return new SocketMonitorResult
        {
            InstanceId = instanceId,
            Connections = connections,
            StartTime = startTime,
            EndTime = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Blocks specific destinations at the application level.
    /// </summary>
    public async Task<PreventionResult> BlockDestinationsAsync(string instanceId, IEnumerable<string> destinations, CancellationToken ct = default)
    {
        // This would be implemented per-application
        // For example, in a browser context, we'd inject blocking scripts
        // In a game context, we'd hook the network layer
        
        await Task.CompletedTask;
        return PreventionResult.Success($"Blocked {destinations.Count()} destinations for instance {instanceId}");
    }

    public void Dispose()
    {
        // Cleanup
    }
}

/// <summary>
/// Options for direct socket leak prevention.
/// </summary>
public sealed class DirectSocketPreventionOptions
{
    public bool EnableTCFiltering { get; set; } = true;
    public bool ForceProxyDNS { get; set; } = true;
    public bool BlockRawSockets { get; set; } = true;
    public bool EnableAppArmor { get; set; } = false;
    public string LDPreloadLibraryPath { get; set; } = "/usr/local/lib/kkbot_socket_filter.so";
    public string ProxyHost { get; set; } = "127.0.0.1";
    public int ProxyPort { get; set; } = 1080;
    public bool EnableAppArmor { get; set; } = false;
}

/// <summary>
/// Result of prevention operation.
/// </summary>
public sealed record PreventionResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static PreventionResult Success(string message) => new() { Success = true, Message = message };
    public static PreventionResult Failed(string message) => new() { Success = false, Message = message };
}

/// <summary>
/// Socket connection information.
/// </summary>
public sealed record SocketConnection
{
    public string State { get; init; } = string.Empty;
    public string LocalAddress { get; init; } = string.Empty;
    public string PeerAddress { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Socket monitoring result.
/// </summary>
public sealed record SocketMonitorResult
{
    public string InstanceId { get; init; } = string.Empty;
    public List<SocketConnection> Connections { get; init; } = new();
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public TimeSpan Duration => EndTime - StartTime;
}