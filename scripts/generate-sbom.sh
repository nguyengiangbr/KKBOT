#!/bin/bash
# KKBOT SBOM Generation Script
# Generates Software Bill of Materials in CycloneDX format

set -e

echo "=== KKBOT SBOM Generation ==="
echo ""

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${PROJECT_ROOT}/artifacts/sbom"
TIMESTAMP=$(date -u +"%Y%m%d-%H%M%S")
OUTPUT_FILE="${OUTPUT_DIR}/kbkot-sbom-${TIMESTAMP}.json"

# Create output directory
mkdir -p "${OUTPUT_DIR}"

echo "Project Root: ${PROJECT_ROOT}"
echo "Output Directory: ${OUTPUT_DIR}"
echo "Output File: ${OUTPUT_FILE}"
echo ""

# Check for required tools
check_tool() {
    if ! command -v "$1" &> /dev/null; then
        echo "ERROR: $1 is not installed"
        return 1
    fi
    echo "Found: $1"
    return 0
}

echo "Checking required tools..."
check_tool "dotnet" || exit 1
check_tool "cyclonedx" || echo "WARNING: cyclonedx not found, will use dotnet-cyclonedx"
check_tool "dotnet-cyclonedx" || echo "WARNING: dotnet-cyclonedx not found, installing..."
dotnet tool install --global CycloneDX 2>/dev/null || true
check_tool "cyclonedx" || check_tool "dotnet-cyclonedx" || { echo "ERROR: CycloneDX tool not available"; exit 1; }

echo ""

# Generate SBOM for .NET solution
echo "Generating SBOM for KKBOT.sln..."
cd "${PROJECT_ROOT}"

# Use dotnet-cyclonedx to generate SBOM for the solution
if command -v cyclonedx &> /dev/null; then
    cyclonedx dotnet -i KKBOT.sln -o "${OUTPUT_FILE}" --format json
elif command -v dotnet-cyclonedx &> /dev/null; then
    dotnet cyclonedx -i KKBOT.sln -o "${OUTPUT_FILE}" --format json
else
    echo "ERROR: No CycloneDX tool available"
    exit 1
fi

# Verify output
if [ -f "${OUTPUT_FILE}" ]; then
    echo ""
    echo "SBOM generated successfully: ${OUTPUT_FILE}"
    echo "File size: $(du -h "${OUTPUT_FILE}" | cut -f1)"
    
    # Validate JSON
    if command -v jq &> /dev/null; then
        if jq empty "${OUTPUT_FILE}" 2>/dev/null; then
            echo "JSON validation: PASSED"
            
            # Show summary
            echo ""
            echo "SBOM Summary:"
            jq -r '.components | length' "${OUTPUT_FILE}" | xargs -I {} echo "  Components: {}"
            jq -r '.dependencies | length' "${OUTPUT_FILE}" | xargs -I {} echo "  Dependencies: {}"
            jq -r '.metadata.component.name' "${OUTPUT_FILE}" | xargs -I {} echo "  Project: {}"
            jq -r '.metadata.component.version' "${OUTPUT_FILE}" | xargs -I {} echo "  Version: {}"
        else
            echo "ERROR: Invalid JSON in SBOM"
            exit 1
        fi
    fi
else
    echo "ERROR: SBOM generation failed"
    exit 1
fi

# Also generate SPDX format if needed
if command -v cyclonedx &> /dev/null; then
    SPDX_FILE="${OUTPUT_DIR}/kbkot-sbom-${TIMESTAMP}.spdx.json"
    echo ""
    echo "Generating SPDX format..."
    cyclonedx dotnet -i KKBOT.sln -o "${SPDX_FILE}" --format spdx
    echo "SPDX format generated: ${SPDX_FILE}"
fi

echo ""
echo "=== SBOM Generation Complete ==="
echo "Output files:"
ls -la "${OUTPUT_DIR}/kbkot-sbom-${TIMESTAMP}"* 2>/dev/null || true
