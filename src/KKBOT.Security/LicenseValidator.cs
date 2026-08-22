// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace KKBOT.Security;

public sealed class LicenseValidator
{
    private readonly ILogger<LicenseValidator> _logger;
    public LicenseValidator(ILogger<LicenseValidator> logger) => _logger = logger;
    public bool Validate(string licenseKey)
    {
        _logger.LogInformation("Validating license key");
        // Placeholder – always true.
        return !string.IsNullOrWhiteSpace(licenseKey);
    }
}