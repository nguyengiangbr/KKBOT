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
/// WebRTC leak prevention for browser-based automation.
/// </summary>
public sealed class WebRTCLeakPrevention
{
    private readonly ILogger<WebRTCLeakPrevention> _logger;
    private readonly WebRTCPreventionOptions _options;

    public WebRTCLeakPrevention(
        ILogger<WebRTCLeakPrevention> logger,
        IOptions<WebRTCPreventionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Configures browser launch arguments to disable WebRTC.
    /// </summary>
    public BrowserLaunchConfig ConfigureBrowserLaunch(BrowserLaunchConfig config, string instanceId)
    {
        var args = new List<string>(config.Args ?? new string[0]);

        if (_options.DisableWebRTC)
        {
            // Chrome/Chromium flags to disable WebRTC
            args.Add("--disable-webrtc");
            args.Add("--disable-webrtc-hw-encoding");
            args.Add("--disable-webrtc-hw-decoding");
            args.Add("--force-webrtc-ip-handling-policy=disable_non_proxied_udp");
            
            _logger.LogDebug("Disabled WebRTC for instance {InstanceId}", instanceId);
        }

        if (_options.ForceProxyForWebRTC)
        {
            // Force WebRTC to use proxy
            args.Add("--force-webrtc-ip-handling-policy=default_public_interface_only");
        }

        // Disable other leak vectors
        if (_options.DisableWebGL)
        {
            args.Add("--disable-webgl");
            args.Add("--disable-webgl2");
        }

        if (_options.DisableCanvasFingerprinting)
        {
            args.Add("--disable-canvas-aa");
            args.Add("--disable-2d-canvas-clip-aa");
        }

        // Disable media devices that could leak info
        if (_options.DisableMediaDevices)
        {
            args.Add("--disable-media-devices");
            args.Add("--disable-microphone");
            args.Add("--disable-camera");
        }

        // Disable geolocation
        if (_options.DisableGeolocation)
        {
            args.Add("--disable-geolocation");
        }

        // Disable speech recognition
        if (_options.DisableSpeechRecognition)
        {
            args.Add("--disable-speech-recognition");
        }

        // Disable battery status API
        if (_options.DisableBatteryStatus)
        {
            args.Add("--disable-battery-status");
        }

        return config with { Args = args.ToArray() };
    }

    /// <summary>
    /// Injects WebRTC blocking script into browser context.
    /// </summary>
    public string GetWebRTCBlockingScript()
    {
        return @"
            // Override RTCPeerConnection to prevent leaks
            (function() {
                'use strict';
                
                // Store original constructors
                const OriginalRTCPeerConnection = window.RTCPeerConnection;
                const OriginalRTCSessionDescription = window.RTCSessionDescription;
                const OriginalRTCIceCandidate = window.RTCIceCandidate;
                
                // Override RTCPeerConnection
                window.RTCPeerConnection = function(configuration) {
                    // Force proxy usage by modifying configuration
                    if (configuration && configuration.iceServers) {
                        // Force all ICE candidates through proxy
                        configuration.iceTransportPolicy = 'relay';
                    }
                    
                    const pc = new OriginalRTCPeerConnection(configuration);
                    
                    // Override createOffer to force proxy
                    const originalCreateOffer = pc.createOffer.bind(pc);
                    pc.createOffer = function(options) {
                        if (options) {
                            options.iceRestart = false;
                        }
                        return originalCreateOffer(options);
                    };
                    
                    // Override createAnswer
                    const originalCreateAnswer = pc.createAnswer.bind(pc);
                    pc.createAnswer = function(options) {
                        return originalCreateAnswer(options);
                    };
                    
                    // Override setLocalDescription to sanitize SDP
                    const originalSetLocalDescription = pc.setLocalDescription.bind(pc);
                    pc.setLocalDescription = function(description) {
                        if (description && description.sdp) {
                            description.sdp = sanitizeSDP(description.sdp);
                        }
                        return originalSetLocalDescription(description);
                    };
                    
                    return pc;
                };
                
                // Copy static properties
                Object.setPrototypeOf(window.RTCPeerConnection, OriginalRTCPeerConnection);
                Object.defineProperties(window.RTCPeerConnection, 
                    Object.getOwnPropertyDescriptors(OriginalRTCPeerConnection));
                
                // Sanitize SDP to remove local IP addresses
                function sanitizeSDP(sdp) {
                    // Remove local IP addresses from SDP
                    return sdp
                        .replace(/a=candidate:.* \d+ \d+ \d+ \d+ \d+ \d+ \d+ \d+ typ host/g, '')
                        .replace(/c=IN IP4 \d+\.\d+\.\d+\.\d+/g, 'c=IN IP4 0.0.0.0')
                        .replace(/a=rtcp:\d+ IN IP4 \d+\.\d+\.\d+\.\d+/g, '');
                }
                
                // Override getUserMedia to prevent device enumeration
                if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
                    const originalGetUserMedia = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
                    navigator.mediaDevices.getUserMedia = function(constraints) {
                        return Promise.reject(new DOMException('getUserMedia blocked for privacy', 'NotAllowedError'));
                    };
                }
                
                // Override enumerateDevices
                if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
                    const originalEnumerateDevices = navigator.mediaDevices.enumerateDevices.bind(navigator.mediaDevices);
                    navigator.mediaDevices.enumerateDevices = async function() {
                        const devices = await originalEnumerateDevices();
                        return devices.filter(d => d.kind !== 'videoinput' && d.kind !== 'audioinput');
                    };
                }
                
                // Block WebRTC data channels
                if (window.RTCDataChannel) {
                    const OriginalRTCDataChannel = window.RTCDataChannel;
                    window.RTCDataChannel = function() {
                        throw new Error('RTCDataChannel blocked for privacy');
                    };
                }
                
                console.log('[KKBOT] WebRTC leak prevention active');
            })();
        ";
    }

    /// <summary>
    /// Generates Chrome/Chromium launch arguments for WebRTC prevention.
    /// </summary>
    public string[] GetChromeLaunchArgs()
    {
        var args = new List<string>();

        if (_options.DisableWebRTC)
        {
            args.Add("--disable-webrtc");
            args.Add("--disable-webrtc-hw-encoding");
            args.Add("--disable-webrtc-hw-decoding");
            args.Add("--force-webrtc-ip-handling-policy=disable_non_proxied_udp");
        }

        if (_options.ForceProxyForWebRTC)
        {
            args.Add("--force-webrtc-ip-handling-policy=default_public_interface_only");
        }

        if (_options.DisableWebGL)
        {
            args.Add("--disable-webgl");
            args.Add("--disable-webgl2");
        }

        if (_options.DisableCanvasFingerprinting)
        {
            args.Add("--disable-canvas-aa");
            args.Add("--disable-2d-canvas-clip-aa");
        }

        if (_options.DisableMediaDevices)
        {
            args.Add("--disable-media-devices");
            args.Add("--disable-microphone");
            args.Add("--disable-camera");
        }

        if (_options.DisableGeolocation)
        {
            args.Add("--disable-geolocation");
        }

        if (_options.DisableSpeechRecognition)
        {
            args.Add("--disable-speech-recognition");
        }

        if (_options.DisableBatteryStatus)
        {
            args.Add("--disable-battery-status");
        }

        // Additional privacy flags
        args.Add("--disable-client-side-phishing-detection");
        args.Add("--disable-component-extensions-with-background-pages");
        args.Add("--disable-default-apps");
        args.Add("--disable-extensions");
        args.Add("--disable-plugins-discovery");
        args.Add("--disable-print-preview");
        args.Add("--disable-sync");
        args.Add("--disable-translate");
        args.Add("--disable-background-networking");
        args.Add("--disable-background-timer-throttling");
        args.Add("--disable-breakpad");
        args.Add("--disable-component-update");
        args.Add("--disable-domain-reliability");
        args.Add("--disable-features=TranslateUI,BlinkGenPropertyTrees");
        args.Add("--disable-hang-monitor");
        args.Add("--disable-ipc-flooding-protection");
        args.Add("--disable-popup-blocking");
        args.Add("--disable-prompt-on-repost");
        args.Add("--disable-renderer-backgrounding");
        args.Add("--disable-sync-preferences");
        args.Add("--metrics-recording-only");
        args.Add("--no-first-run");
        args.Add("--no-default-browser-check");
        args.Add("--no-pings");
        args.Add("--password-store=basic");
        args.Add("--use-mock-keychain");

        return args.ToArray();
    }

    /// <summary>
    /// Generates Firefox launch arguments for WebRTC prevention.
    /// </summary>
    public string[] GetFirefoxLaunchArgs()
    {
        var args = new List<string>();

        if (_options.DisableWebRTC)
        {
            args.Add("-pref");
            args.Add("media.peerconnection.enabled=false");
            args.Add("-pref");
            args.Add("media.peerconnection.ice.no_host=true");
            args.Add("-pref");
            args.Add("media.peerconnection.ice.relay_only=true");
            args.Add("-pref");
            args.Add("media.peerconnection.ice.proxy_only=true");
        }

        if (_options.DisableWebGL)
        {
            args.Add("-pref");
            args.Add("webgl.disabled=true");
        }

        if (_options.DisableMediaDevices)
        {
            args.Add("-pref");
            args.Add("media.navigator.enabled=false");
            args.Add("-pref");
            args.Add("media.navigator.permission.disabled=true");
        }

        if (_options.DisableGeolocation)
        {
            args.Add("-pref");
            args.Add("geo.enabled=false");
        }

        // Privacy preferences
        args.Add("-pref");
        args.Add("privacy.resistFingerprinting=true");
        args.Add("-pref");
        args.Add("privacy.trackingprotection.enabled=true");
        args.Add("-pref");
        args.Add("browser.send_pings=false");
        args.Add("-pref");
        args.Add("network.cookie.cookieBehavior=1");
        args.Add("-pref");
        args.Add("network.http.referer.trimmingPolicy=2");
        args.Add("-pref");
        args.Add("network.http.referer.XOriginPolicy=2");
        args.Add("-pref");
        args.Add("network.http.referer.XOriginTrimmingPolicy=2");
        args.Add("-pref");
        args.Add("network.predictor.enabled=false");
        args.Add("-pref");
        args.Add("network.prefetch-next=false");
        args.Add("-pref");
        args.Add("browser.send_pings=false");
        args.Add("-pref");
        args.Add("beacon.enabled=false");

        return args.ToArray();
    }

    /// <summary>
    /// Checks if current browser configuration has WebRTC leaks.
    /// </summary>
    public async Task<WebRTCLeakTestResult> TestWebRTCLeaksAsync(string browserWsEndpoint, CancellationToken ct = default)
    {
        try
        {
            // Connect to browser via CDP (Chrome DevTools Protocol)
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(browserWsEndpoint), ct);

            // Enable Network domain
            await SendCDPCommand(ws, "Network.enable", new { }, ct);
            
            // Create a test RTCPeerConnection and check for leaks
            var script = @"
                (async () => {
                    try {
                        const pc = new RTCPeerConnection({iceServers: []});
                        pc.createDataChannel('test');
                        const offer = await pc.createOffer();
                        await pc.setLocalDescription(offer);
                        return pc.localDescription.sdp;
                    } catch (e) {
                        return 'ERROR: ' + e.message;
                    }
                })();
            ";

            const result = await EvaluateInBrowser(ws, script, ct);
            
            const hasLeak = result && result.includes('candidate:') && 
                           (result.includes('typ host') || result.includes('typ srflx'));

            return new WebRTCLeakTestResult
            {
                HasLeak = hasLeak,
                SDP = result,
                TestedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new WebRTCLeakTestResult
            {
                HasLeak = true,
                Error = ex.Message,
                TestedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private async Task SendCDPCommand(ClientWebSocket ws, string method, object params_, CancellationToken ct)
    {
        var message = new { id = 1, method, params = params_ };
        var json = System.Text.Json.JsonSerializer.Serialize(message);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private async Task<string> EvaluateInBrowser(ClientWebSocket ws, string script, CancellationToken ct)
    {
        await SendCDPCommand(ws, "Runtime.evaluate", new { expression = script, awaitPromise = true }, ct);
        
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        var json = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
        
        // Parse result
        var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("result").GetProperty("result").GetProperty("value").GetString() ?? "";
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}

/// <summary>
/// WebRTC prevention options.
/// </summary>
public sealed class WebRTCPreventionOptions
{
    public bool DisableWebRTC { get; set; } = true;
    public bool ForceProxyForWebRTC { get; set; } = true;
    public bool DisableWebGL { get; set; } = false;
    public bool DisableCanvasFingerprinting { get; set; } = true;
    public bool DisableMediaDevices { get; set; } = true;
    public bool DisableGeolocation { get; set; } = true;
    public bool DisableSpeechRecognition { get; set; } = true;
    public bool DisableBatteryStatus { get; set; } = true;
}

/// <summary>
/// Browser launch configuration.
/// </summary>
public sealed record BrowserLaunchConfig
{
    public string[] Args { get; init; } = Array.Empty<string>();
    public string ExecutablePath { get; init; } = string.Empty;
    public string UserDataDir { get; init; } = string.Empty;
    public bool Headless { get; init; } = false;
    public Dictionary<string, object> Options { get; init; } = new();
}

/// <summary>
/// WebRTC leak test result.
/// </summary>
public sealed record WebRTCLeakTestResult
{
    public bool HasLeak { get; init; }
    public string? SDP { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset TestedAt { get; init; }
}