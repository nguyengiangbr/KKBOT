// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Orchestration;

public static class ErrorCodes
{
    public const string TASK_TIMEOUT = "TASK_TIMEOUT";
    public const string OCR_FAILED = "OCR_FAILED";
    public const string DETECTOR_LOW_CONFIDENCE = "DETECTOR_LOW_CONFIDENCE";
    public const string TASK_FAILED = "TASK_FAILED";
    public const string APP_CRASHED = "APP_CRASHED";
    public const string APP_NOT_RUNNING = "APP_NOT_RUNNING";
    public const string DEVICE_DISCONNECTED = "DEVICE_DISCONNECTED";
    public const string CAPTURE_FAILED = "CAPTURE_FAILED";
    public const string DEVICE_NOT_FOUND = "DEVICE_NOT_FOUND";
}