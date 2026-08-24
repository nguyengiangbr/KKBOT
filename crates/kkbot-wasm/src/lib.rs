// KKBOT WASM SDK - Rust bindings for KKBOT host functions
// This crate provides safe Rust abstractions for calling KKBOT host functions from WASM plugins.

use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use bitflags::bitflags;

// ============================================================================
// Capability System
// ============================================================================

bitflags::bitflags! {
    /// Plugin capabilities - deny by default, must be declared in manifest
    #[derive(Serialize, Deserialize, Debug, Clone, Copy, PartialEq, Eq, Hash)]
    pub struct Capability: u64 {
        // Device capabilities
        const DeviceRead      = 1 << 0;  // Capture screen, get device info
        const DeviceWrite     = 1 << 1;  // Send input commands
        const DeviceControl   = 1 << 2;  // Launch/stop apps, install packages
        
        // Perception capabilities
        const PerceptionBasic      = 1 << 3;  // Template matching, color analysis
        const PerceptionAdvanced   = 1 << 4;  // Custom detectors, OCR
        const PerceptionTrain      = 1 << 5;  // Train detectors
        
        // AI capabilities
        const AiInference   = 1 << 6;  // Text generation
        const AiVision      = 1 << 7;  // Vision models
        const AiTrain       = 1 << 8;  // Model training
        
        // Storage capabilities
        const StorageRead         = 1 << 9;  // Read plugin assets, config
        const StorageWrite        = 1 << 10; // Write logs, cache, temp files
        const StoragePersistent   = 1 << 11; // Persistent storage across runs
        
        // Network capabilities
        const NetworkHttp      = 1 << 12; // Outbound HTTP requests
        const NetworkWebsocket = 1 << 13; // WebSocket connections
        const NetworkLocal     = 1 << 14; // Localhost only
        
        // System capabilities
        const SystemTime     = 1 << 15; // Access to time/date
        const SystemRandom   = 1 << 16; // Cryptographic random
        const SystemLogging  = 1 << 17; // Structured logging
        
        // Plugin capabilities
        const PluginSpawn       = 1 << 18; // Spawn child plugins
        const PluginCommunicate = 1 << 19; // Inter-plugin communication
    }

impl Capability {
    /// Standard capability set for typical game automation plugins
    pub const STANDARD: Capability = Capability::DeviceRead.union(Capability::DeviceWrite)
        .union(Capability::PerceptionBasic)
        .union(Capability::AiInference)
        .union(Capability::StorageRead)
        .union(Capability::SystemTime)
        .union(Capability::SystemLogging);
    
    /// Full capability set (all permissions)
    pub const FULL: Capability = Capability::from_bits_truncate(u64::MAX);
}

// ============================================================================
// Resource Limits
// ============================================================================

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
pub struct ResourceLimits {
    pub max_memory_bytes: u64,
    pub max_fuel: u64,
    pub max_execution_time_ms: u64,
    pub max_stack_size: u32,
    pub max_table_entries: u32,
    pub max_concurrent_calls: u32,
}

impl ResourceLimits {
    pub fn default() -> Self {
        Self {
            max_memory_bytes: 64 * 1024 * 1024,  // 64 MB
            max_fuel: 10_000_000,                 // ~10M instructions
            max_execution_time_ms: 30_000,        // 30 seconds
            max_stack_size: 512 * 1024,           // 512 KB
            max_table_entries: 1000,
            max_concurrent_calls: 10,
        }
    }
}

// ============================================================================
// Plugin Manifest
// ============================================================================

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PluginManifest {
    pub plugin_id: String,
    pub name: String,
    pub version: String,
    pub author: String,
    pub description: String,
    pub entry_point: String,
    pub required_capabilities: Capability,
    pub resource_limits: ResourceLimits,
    pub metadata: HashMap<String, String>,
    pub required_host_functions: Vec<String>,
    pub sha256_hash: String,
}

impl Default for PluginManifest {
    fn default() -> Self {
        Self {
            plugin_id: String::new(),
            name: String::new(),
            version: "1.0.0".to_string(),
            author: String::new(),
            description: String::new(),
            entry_point: "plugin_init".to_string(),
            required_capabilities: Capability::STANDARD,
            resource_limits: ResourceLimits::default(),
            metadata: HashMap::new(),
            required_host_functions: Vec::new(),
            sha256_hash: String::new(),
        }
    }
}

// ============================================================================
// Host Function Calling
// ============================================================================

/// Call a host function and return the JSON response
pub fn call_host_function(namespace: &str, name: &str, input: &impl Serialize) -> Result<String, HostError> {
    let input_json = serde_json::to_string(input).map_err(HostError::Serialization)?;
    let (out_ptr, out_len) = unsafe {
        syscall_host_function(
            namespace.as_ptr(), namespace.len(),
            name.as_ptr(), name.len(),
            input_json.as_ptr(), input_json.len()
        )
    };
    
    if out_ptr == 0 {
        return Err(HostError::NullPointer);
    }
    
    let result = unsafe { read_string(out_ptr, out_len) };
    Ok(result)
}

/// Low-level syscall to host function (provided by host)
extern "C" {
    fn syscall_host_function(
        ns_ptr: *const u8, ns_len: usize,
        fn_ptr: *const u8, fn_len: usize,
        in_ptr: *const u8, in_len: usize
    ) -> (i32, i32);
    
    fn read_string(ptr: i32, len: i32) -> String;
}

// ============================================================================
// Memory Management
// ============================================================================

/// Allocate memory in WASM linear memory
pub fn malloc(size: usize) -> *mut u8 {
    unsafe { syscall_malloc(size) }
}

/// Free memory allocated by malloc
pub fn free(ptr: *mut u8) {
    unsafe { syscall_free(ptr) }
}

extern "C" {
    fn syscall_malloc(size: usize) -> *mut u8;
    fn syscall_free(ptr: *mut u8);
}

// ============================================================================
// Error Handling
// ============================================================================

#[derive(Debug, thiserror::Error)]
pub enum HostError {
    #[error("Serialization error: {0}")]
    Serialization(#[from] serde_json::Error),
    #[error("Host function returned null pointer")]
    NullPointer,
    #[error("Host function error: {0}")]
    HostError(String),
    #[error("Capability not granted: {0}")]
    CapabilityDenied(String),
}

// ============================================================================
// Device Functions
// ============================================================================

/// Capture screen from device
pub async fn device_capture(input: DeviceCaptureInput) -> Result<DeviceCaptureOutput, HostError> {
    let result = call_host_function("kkbot", "device_capture", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct DeviceCaptureInput {
    pub device_id: String,
    pub region: Option<Region>,
    pub format: Option<String>,
    pub quality: Option<u8>,
}

#[derive(Debug, Serialize)]
pub struct Region {
    pub x: i32,
    pub y: i32,
    pub width: u32,
    pub height: u32,
}

#[derive(Debug, Deserialize)]
pub struct DeviceCaptureOutput {
    pub frame_id: String,
    pub width: u32,
    pub height: u32,
    pub format: String,
    pub data_base64: String,
    pub capture_latency_ms: f64,
}

/// Send input to device
pub async fn device_input(input: DeviceInputInput) -> Result<DeviceInputOutput, HostError> {
    let result = call_host_function("kkbot", "device_input", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct DeviceInputInput {
    pub device_id: String,
    #[serde(rename = "type")]
    pub type_: String,  // "tap", "swipe", "text", "key"
    pub x: i32,
    pub y: i32,
    pub end_x: i32,
    pub end_y: i32,
    pub text: Option<String>,
    pub duration_ms: Option<u64>,
    pub key_code: Option<i32>,
}

#[derive(Debug, Deserialize)]
pub struct DeviceInputOutput {
    pub success: bool,
    pub error_message: Option<String>,
    pub duration_ms: f64,
}

/// Get device information
pub async fn device_info(input: DeviceInfoInput) -> Result<DeviceInfoOutput, HostError> {
    let result = call_host_function("kkbot", "device_info", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct DeviceInfoInput {
    pub device_id: String,
}

#[derive(Debug, Deserialize)]
pub struct DeviceInfoOutput {
    pub device_id: String,
    pub model: String,
    pub manufacturer: String,
    pub android_version: String,
    pub screen_width: u32,
    pub screen_height: u32,
    pub screen_density: u32,
    pub battery_level: Option<u32>,
    pub is_charging: Option<bool>,
}

// ============================================================================
// Perception Functions
// ============================================================================

/// Run template detection on frame
pub async fn perception_detect(input: PerceptionDetectInput) -> Result<PerceptionDetectOutput, HostError> {
    let result = call_host_function("kkbot", "perception_detect", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct PerceptionDetectInput {
    pub frame_base64: String,
    pub width: u32,
    pub height: u32,
    pub template_names: Option<Vec<String>>,
}

#[derive(Debug, Deserialize)]
pub struct PerceptionDetectOutput {
    pub detections: Vec<DetectionOutput>,
}

#[derive(Debug, Deserialize)]
pub struct DetectionOutput {
    pub detector_id: String,
    pub detector_name: String,
    pub confidence: f64,
    pub is_match: bool,
    pub detected_value: Option<String>,
    pub bounding_box: Option<BoundingBox>,
}

#[derive(Debug, Deserialize)]
pub struct BoundingBox {
    pub x: i32,
    pub y: i32,
    pub width: u32,
    pub height: u32,
}

/// Run OCR on frame region
pub async fn perception_ocr(input: PerceptionOcrInput) -> Result<PerceptionOcrOutput, HostError> {
    let result = call_host_function("kkbot", "perception_ocr", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct PerceptionOcrInput {
    pub frame_base64: String,
    pub width: u32,
    pub height: u32,
    pub x: Option<u32>,
    pub y: Option<u32>,
    pub width: Option<u32>,
    pub height: Option<u32>,
}

#[derive(Debug, Deserialize)]
pub struct PerceptionOcrOutput {
    pub text: String,
    pub confidence: f64,
    pub error: Option<String>,
}

// ============================================================================
// AI Functions
// ============================================================================

/// Generate text using AI model
pub async fn ai_generate(input: AiGenerateInput) -> Result<AiGenerateOutput, HostError> {
    let result = call_host_function("kkbot", "ai_generate", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct AiGenerateInput {
    pub prompt: String,
    pub model: Option<String>,
    pub temperature: Option<f64>,
    pub max_tokens: Option<u32>,
}

#[derive(Debug, Deserialize)]
pub struct AiGenerateOutput {
    pub content: String,
    pub tokens: u32,
    pub duration_ms: u32,
}

/// Analyze image with vision model
pub async fn ai_vision(input: AiVisionInput) -> Result<AiVisionOutput, HostError> {
    let result = call_host_function("kkbot", "ai_vision", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct AiVisionInput {
    pub image_base64: String,
    pub prompt: String,
    pub model: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct AiVisionOutput {
    pub content: String,
    pub tokens: u32,
    pub duration_ms: u32,
    pub error: Option<String>,
}

// ============================================================================
// Storage Functions
// ============================================================================

/// Read file from plugin storage
pub async fn storage_read(input: StorageReadInput) -> Result<StorageReadOutput, HostError> {
    let result = call_host_function("kkbot", "storage_read", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct StorageReadInput {
    pub path: String,
}

#[derive(Debug, Deserialize)]
pub struct StorageReadOutput {
    pub success: bool,
    pub data_base64: Option<String>,
    pub size: u64,
    pub error: Option<String>,
}

/// Write file to plugin storage
pub async fn storage_write(input: StorageWriteInput) -> Result<StorageWriteOutput, HostError> {
    let result = call_host_function("kkbot", "storage_write", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct StorageWriteInput {
    pub path: String,
    pub data_base64: String,
}

#[derive(Debug, Deserialize)]
pub struct StorageWriteOutput {
    pub success: bool,
    pub size: u64,
    pub error: Option<String>,
}

/// List files in plugin storage
pub async fn storage_list(input: StorageListInput) -> Result<StorageListOutput, HostError> {
    let result = call_host_function("kkbot", "storage_list", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct StorageListInput {
    pub prefix: Option<String>,
    pub recursive: bool,
}

#[derive(Debug, Deserialize)]
pub struct StorageListOutput {
    pub success: bool,
    pub files: Vec<StorageFileInfo>,
    pub error: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct StorageFileInfo {
    pub path: String,
    pub size: u64,
    pub modified_at: String, // ISO 8601
}

// ============================================================================
// Network Functions
// ============================================================================

/// Make HTTP request
pub async fn network_http(input: NetworkHttpInput) -> Result<NetworkHttpOutput, HostError> {
    let result = call_host_function("kkbot", "network_http", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct NetworkHttpInput {
    pub url: String,
    pub method: String,
    pub headers: Option<HashMap<String, String>>,
    pub body_base64: Option<String>,
    pub content_type: Option<String>,
    pub timeout_seconds: Option<u32>,
}

#[derive(Debug, Deserialize)]
pub struct NetworkHttpOutput {
    pub success: bool,
    pub status_code: u16,
    pub headers: Option<HashMap<String, String>>,
    pub body_base64: Option<String>,
    pub error: Option<String>,
}

// ============================================================================
// System Functions
// ============================================================================

/// Get current UTC timestamp in milliseconds
pub async fn system_time() -> Result<SystemTimeOutput, HostError> {
    let result = call_host_function("kkbot", "system_time", &())?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Deserialize)]
pub struct SystemTimeOutput {
    pub timestamp_ms: i64,
}

/// Generate cryptographically secure random bytes
pub async fn system_random(length: u32) -> Result<SystemRandomOutput, HostError> {
    let input = SystemRandomInput { length };
    let result = call_host_function("kkbot", "system_random", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct SystemRandomInput {
    pub length: u32,
}

#[derive(Debug, Deserialize)]
pub struct SystemRandomOutput {
    pub data_base64: String,
}

/// Record custom metric
pub async fn system_metrics(input: SystemMetricsInput) -> Result<SystemMetricsOutput, HostError> {
    let result = call_host_function("kkbot", "system_metrics", &input)?;
    serde_json::from_str(&result).map_err(HostError::Serialization)
}

#[derive(Debug, Serialize)]
pub struct SystemMetricsInput {
    pub name: String,
    pub value: serde_json::Value,
    pub tags: Option<HashMap<String, String>>,
}

#[derive(Debug, Deserialize)]
pub struct SystemMetricsOutput {
    pub success: bool,
}

// ============================================================================
// Logging Functions
// ============================================================================

/// Log info message
pub fn log_info(message: &str, fields: Option<HashMap<String, serde_json::Value>>) {
    let input = LogInput { message: message.to_string(), fields };
    let _ = call_host_function("kkbot", "log_info", &input);
}

/// Log warning message
pub fn log_warn(message: &str, fields: Option<HashMap<String, serde_json::Value>>) {
    let input = LogInput { message: message.to_string(), fields };
    let _ = call_host_function("kkbot", "log_warn", &input);
}

/// Log error message
pub fn log_error(message: &str, fields: Option<HashMap<String, serde_json::Value>>) {
    let input = LogInput { message: message.to_string(), fields };
    let _ = call_host_function("kkbot", "log_error", &input);
}

#[derive(Debug, Serialize)]
struct LogInput {
    message: String,
    fields: Option<HashMap<String, serde_json::Value>>,
}

// ============================================================================
// Plugin Lifecycle
// ============================================================================

/// Plugin initialization result
#[derive(Debug, Serialize)]
pub struct PluginInitResult {
    pub success: bool,
    pub error: Option<String>,
    pub exported_functions: Vec<String>,
    pub declared_capabilities: Capability,
    pub declared_limits: ResourceLimits,
}

/// Helper to write JSON to WASM memory and return (ptr, len)
pub fn write_json<T: Serialize>(value: &T) -> (i32, i32) {
    let json = serde_json::to_string(value).unwrap();
    let bytes = json.as_bytes();
    let ptr = unsafe { malloc(bytes.len()) };
    unsafe {
        std::slice::from_raw_parts_mut(ptr as *mut u8, bytes.len()).copy_from_slice(bytes);
    }
    (ptr, bytes.len() as i32)
}

/// Read string from WASM memory
unsafe fn read_string(ptr: i32, len: i32) -> String {
    // This is provided by the host
    extern "C" {
        fn read_string(ptr: i32, len: i32) -> String;
    }
    read_string(ptr, len)
}

/// Allocate memory in WASM linear memory
extern "C" {
    fn malloc(size: usize) -> *mut u8;
    fn free(ptr: *mut u8);
}