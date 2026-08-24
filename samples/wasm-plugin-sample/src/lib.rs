// Sample KKBOT WASM Plugin in Rust
// This demonstrates how to write a game automation plugin using the KKBOT WASM SDK.

use kkbot_wasm::*;
use serde::{Deserialize, Serialize};
use std::sync::OnceLock;

// Plugin metadata
#[no_mangle]
pub extern "C" fn plugin_init(ptr: i32, len: i32) -> (i32, i32) {
    let manifest_json = unsafe { read_string(ptr, len) };
    let manifest: PluginManifest = serde_json::from_str(&manifest_json).unwrap();
    
    // Store manifest for later use
    MANIFEST.set(manifest).unwrap();
    
    // Return init result
    let result = PluginInitResult {
        success: true,
        error: None,
        exported_functions: vec![
            "sample_detect_health".to_string(),
            "sample_auto_heal".to_string(),
            "sample_get_state".to_string(),
        ],
        declared_capabilities: Capability::DeviceRead as u64 | Capability::DeviceWrite as u64 | Capability::PerceptionBasic as u64,
        declared_limits: ResourceLimits::default(),
    };
    
    write_json(&result)
}

// Plugin manifest storage
static MANIFEST: OnceLock<PluginManifest> = OnceLock::new();

// Detect health bar using template matching
#[no_mangle]
pub extern "C" fn sample_detect_health(ptr: i32, len: i32) -> (i32, i32) {
    let input_json = unsafe { read_string(ptr, len) };
    let input: DetectHealthInput = serde_json::from_str(&input_json).unwrap();
    
    // Call host function: kkbot::perception_detect
    let detect_input = PerceptionDetectInput {
        frame_base64: input.frame_base64,
        width: input.width,
        height: input.height,
        template_names: Some(vec!["health_bar".to_string(), "health_low".to_string()]),
    };
    
    let detect_output = host_call("kkbot::perception_detect", &detect_input).unwrap();
    let detections: PerceptionDetectOutput = serde_json::from_str(&detect_output).unwrap();
    
    // Find health bar detection
    let health = detections.detections.iter()
        .find(|d| d.detector_name == "health_bar" || d.detector_name == "health_low")
        .cloned();
    
    let output = DetectHealthOutput {
        found: health.is_some(),
        confidence: health.map(|h| h.confidence).unwrap_or(0.0),
        bounding_box: health.and_then(|h| h.bounding_box),
    };
    
    write_json(&output)
}

// Auto-heal logic: detect low health, use potion
#[no_mangle]
pub extern "C" fn sample_auto_heal(ptr: i32, len: i32) -> (i32, i32) {
    let input_json = unsafe { read_string(ptr, len) };
    let input: AutoHealInput = serde_json::from_str(&input_json).unwrap();
    
    // Check health
    let detect_input = DetectHealthInput {
        frame_base64: input.frame_base64,
        width: input.width,
        height: input.height,
    };
    let detect_output = sample_detect_health(write_json(&detect_input).0, write_json(&detect_input).1);
    let health: DetectHealthOutput = serde_json::from_str(&unsafe { read_string(detect_output.0, detect_output.1) }).unwrap();
    
    if health.found && health.confidence > input.threshold {
        // Check if health is low (confidence indicates health bar detected)
        // In real implementation, you'd parse the health percentage from OCR
        
        // Use health potion (key '1' or click potion slot)
        let input_cmd = DeviceInputInput {
            device_id: input.device_id,
            type_: "key".to_string(),
            x: 0,
            y: 0,
            end_x: 0,
            end_y: 0,
            text: None,
            duration_ms: Some(50),
            key_code: Some(input.potion_key_code),
        };
        
        let input_result = host_call("kkbot::device_input", &input_cmd).unwrap();
        let result: DeviceInputOutput = serde_json::from_str(&input_result).unwrap();
        
        let output = AutoHealOutput {
            healed: result.success,
            error: result.error_message,
        };
        
        write_json(&output)
    } else {
        write_json(&AutoHealOutput { healed: false, error: Some("Health not low or detection failed".to_string()) })
    }
}

// Get plugin state
#[no_mangle]
pub extern "C" fn sample_get_state(ptr: i32, len: i32) -> (i32, i32) {
    let input_json = unsafe { read_string(ptr, len) };
    let _input: GetStateInput = serde_json::from_str(&input_json).unwrap();
    
    let output = GetStateOutput {
        plugin_name: "Sample Health Plugin".to_string(),
        version: "1.0.0".to_string(),
        status: "running".to_string(),
    };
    
    write_json(&output)
}

// Plugin cleanup
#[no_mangle]
pub extern "C" fn plugin_cleanup() {
    // Cleanup resources if needed
}

// Host function calling helper
fn host_call(namespace: &str, name: &str, input: &impl Serialize) -> Result<String, String> {
    let input_json = serde_json::to_string(input).map_err(|e| e.to_string())?;
    let (out_ptr, out_len) = unsafe {
        call_host_function(namespace, name, write_json(input).0, write_json(input).1)
    };
    let result = unsafe { read_string(out_ptr, out_len) };
    Ok(result)
}

// Memory management (provided by host)
extern "C" {
    fn malloc(size: usize) -> i32;
    fn free(ptr: i32);
    fn call_host_function(ns_ptr: i32, ns_len: i32, fn_ptr: i32, fn_len: i32, in_ptr: i32, in_len: i32) -> (i32, i32);
    fn read_string(ptr: i32, len: i32) -> String;
}

// Helper to write JSON to WASM memory
fn write_json<T: Serialize>(value: &T) -> (i32, i32) {
    let json = serde_json::to_string(value).unwrap();
    let bytes = json.as_bytes();
    let ptr = unsafe { malloc(bytes.len()) };
    unsafe {
        std::slice::from_raw_parts_mut(ptr as *mut u8, bytes.len()).copy_from_slice(bytes);
    }
    (ptr, bytes.len() as i32)
}

// Data types
#[derive(Deserialize, Serialize)]
struct PluginManifest {
    plugin_id: String,
    name: String,
    version: String,
    author: String,
    description: String,
    entry_point: String,
    required_capabilities: u64,
    resource_limits: ResourceLimits,
    metadata: std::collections::HashMap<String, String>,
    required_host_functions: Vec<String>,
    sha256_hash: String,
}

#[derive(Deserialize, Serialize, Default)]
struct ResourceLimits {
    max_memory_bytes: u64,
    max_fuel: u64,
    max_execution_time_ms: u64,
    max_stack_size: u32,
    max_table_entries: u32,
    max_concurrent_calls: u32,
}

#[derive(Serialize)]
struct PluginInitResult {
    success: bool,
    error: Option<String>,
    exported_functions: Vec<String>,
    declared_capabilities: u64,
    declared_limits: ResourceLimits,
}

#[derive(Deserialize)]
struct DetectHealthInput {
    frame_base64: String,
    width: u32,
    height: u32,
}

#[derive(Serialize)]
struct DetectHealthOutput {
    found: bool,
    confidence: f64,
    bounding_box: Option<BoundingBox>,
}

#[derive(Serialize)]
struct BoundingBox {
    x: i32,
    y: i32,
    width: u32,
    height: u32,
}

#[derive(Deserialize, Serialize)]
struct PerceptionDetectInput {
    frame_base64: String,
    width: u32,
    height: u32,
    template_names: Option<Vec<String>>,
}

#[derive(Deserialize)]
struct PerceptionDetectOutput {
    detections: Vec<DetectionOutput>,
}

#[derive(Deserialize)]
struct DetectionOutput {
    detector_id: String,
    detector_name: String,
    confidence: f64,
    is_match: bool,
    detected_value: Option<String>,
    bounding_box: Option<BoundingBox>,
}

#[derive(Deserialize)]
struct AutoHealInput {
    frame_base64: String,
    width: u32,
    height: u32,
    threshold: f64,
    device_id: String,
    potion_key_code: i32,
}

#[derive(Serialize)]
struct AutoHealOutput {
    healed: bool,
    error: Option<String>,
}

#[derive(Deserialize)]
struct GetStateInput {}

#[derive(Serialize)]
struct GetStateOutput {
    plugin_name: String,
    version: String,
    status: String,
}

#[derive(Deserialize, Serialize)]
struct DeviceInputInput {
    device_id: String,
    type_: String,
    x: i32,
    y: i32,
    end_x: i32,
    end_y: i32,
    text: Option<String>,
    duration_ms: Option<u64>,
    key_code: Option<i32>,
}

#[derive(Deserialize)]
struct DeviceInputOutput {
    success: bool,
    error_message: Option<String>,
    duration_ms: f64,
}

bitflags::bitflags! {
    #[derive(Serialize, Deserialize)]
    struct Capability: u64 {
        const DeviceRead = 1 << 0;
        const DeviceWrite = 1 << 1;
        const DeviceControl = 1 << 2;
        const PerceptionBasic = 1 << 3;
        const PerceptionAdvanced = 1 << 4;
        const PerceptionTrain = 1 << 5;
        const AiInference = 1 << 6;
        const AiVision = 1 << 7;
        const AiTrain = 1 << 8;
        const StorageRead = 1 << 9;
        const StorageWrite = 1 << 10;
        const StoragePersistent = 1 << 11;
        const NetworkHttp = 1 << 12;
        const NetworkWebsocket = 1 << 13;
        const NetworkLocal = 1 << 14;
        const SystemTime = 1 << 15;
        const SystemRandom = 1 << 16;
        const SystemLogging = 1 << 17;
        const PluginSpawn = 1 << 18;
        const PluginCommunicate = 1 << 19;
    }
}