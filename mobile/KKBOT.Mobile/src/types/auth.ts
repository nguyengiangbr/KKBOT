export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  avatarUrl?: string;
  tenantId?: string;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
  roles: string[];
  permissions: string[];
}

export interface AuthTokens {
  accessToken: string;
  expiresIn: number;
  tokenType: string;
  refreshToken?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface TokenResponse {
  accessToken: string;
  expiresIn: number;
  tokenType: string;
  user: User;
}

// Instance types
export interface Instance {
  id: string;
  name: string;
  status: InstanceStatus;
  deviceId?: string;
  moduleId?: string;
  configuration?: string;
  createdAt: string;
  updatedAt?: string;
  module?: ModuleRef;
  device?: DeviceRef;
}

export type InstanceStatus = 'Created' | 'Running' | 'Stopped' | 'Error' | 'Paused';

export interface ModuleRef {
  id: string;
  name: string;
  version: string;
}

export interface DeviceRef {
  id: string;
  name: string;
  type: string;
}

// Job types
export interface Job {
  id: string;
  instanceId: string;
  type: JobType;
  status: JobStatus;
  priority: number;
  payload: string;
  result?: string;
  errorMessage?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  updatedAt: string;
}

export type JobType = 'OneShot' | 'Repeating' | 'Manual';
export type JobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

// Plugin types
export interface Plugin {
  id: string;
  name: string;
  version: string;
  author: string;
  description: string;
  requiredPermissions: string[];
  supportedResolutions: Resolution[];
  detectors: DetectorInfo[];
  tasks: TaskInfo[];
  states: StateInfo[];
}

export interface Resolution {
  width: number;
  height: number;
}

export interface DetectorInfo {
  id: string;
  name: string;
}

export interface TaskInfo {
  id: string;
  name: string;
  type: string;
}

export interface StateInfo {
  id: string;
  name: string;
  category: string;
}

// Marketplace types
export interface MarketplacePlugin {
  pluginId: string;
  name: string;
  version: string;
  author: string;
  description: string;
  tags: string[];
  repositoryUrl: string;
  downloadUrl: string;
  iconUrl: string;
  manifest: PluginManifest;
  publishedAt: string;
  downloadCount: number;
  rating: number;
  ratingCount: number;
  verified: boolean;
  supportedPlatforms: string[];
}

export interface PluginManifest {
  id: string;
  name: string;
  version: string;
  author: string;
  description: string;
  entryAssembly: string;
  assets: string[];
  dependencies: PluginDependency[];
  configSchema: string;
  defaultConfig: string;
  supportedResolutions: Resolution[];
  requiredPermissions: string[];
}

export interface PluginDependency {
  pluginId: string;
  versionRange: string;
  isOptional: boolean;
}

export interface InstallResult {
  success: boolean;
  pluginId?: string;
  version?: string;
  errorMessage?: string;
  installedDependencies?: string[];
}

// AI Types
export interface ModelMetadata {
  modelId: string;
  name: string;
  version: string;
  type: ModelType;
  framework: ModelFramework;
  description: string;
  author: string;
  tags: Record<string, string>;
  metrics: ModelMetrics;
  inputSchema: ModelSchema;
  outputSchema: ModelSchema;
  hyperparameters: Record<string, any>;
  createdAt: string;
  updatedAt: string;
}

export type ModelType = 'Classification' | 'Regression' | 'TextGeneration' | 'ImageClassification' | 'ObjectDetection' | 'Embedding' | 'Custom';
export type ModelFramework = 'ONNX' | 'TensorFlow' | 'PyTorch' | 'MLNet' | 'Ollama' | 'HuggingFace' | 'Custom';
export type ModelStatus = 'Draft' | 'Training' | 'Validating' | 'Ready' | 'Deployed' | 'Archived' | 'Failed';

export interface ModelSchema {
  name: string;
  dataType: string;
  shape: number[];
  description?: string;
}

export interface ModelMetrics {
  accuracy: number;
  precision: number;
  recall: number;
  f1Score: number;
  loss: number;
  customMetrics: Record<string, number>;
  evaluatedAt: string;
  datasetVersion: string;
}

export interface TrainingJob {
  jobId: string;
  modelId: string;
  name: string;
  config: TrainingConfig;
  status: TrainingStatus;
  progress: TrainingProgress;
  errorMessage?: string;
  startedAt: string;
  completedAt?: string;
  checkpointPath?: string;
  artifacts: Record<string, any>;
}

export type TrainingStatus = 'Queued' | 'Preparing' | 'Running' | 'Paused' | 'Completed' | 'Failed' | 'Cancelled';

export interface TrainingConfig {
  datasetPath: string;
  validationDatasetPath: string;
  epochs: number;
  batchSize: number;
  learningRate: number;
  optimizer: string;
  lossFunction: string;
  hyperparameters: Record<string, any>;
  earlyStopping: boolean;
  earlyStoppingPatience: number;
  checkpointDir?: string;
  metricsToTrack: string[];
}

export interface TrainingProgress {
  currentEpoch: number;
  totalEpochs: number;
  currentBatch: number;
  totalBatches: number;
  currentLoss: number;
  currentMetrics: Record<string, number>;
  estimatedTimeRemainingSeconds: number;
}

export interface InferenceRequest {
  requestId: string;
  modelId: string;
  modelVersion: string;
  inputs: Record<string, any>;
  options: InferenceOptions;
}

export interface InferenceOptions {
  returnProbabilities?: boolean;
  topK?: number;
  temperature?: number;
  maxTokens?: number;
  customOptions?: Record<string, any>;
}

export interface InferenceResponse {
  requestId: string;
  modelId: string;
  modelVersion: string;
  outputs: Record<string, any>;
  metrics: InferenceMetrics;
  success: boolean;
  errorMessage?: string;
}

export interface InferenceMetrics {
  latencyMs: number;
  queueTimeMs: number;
  preprocessTimeMs: number;
  inferenceTimeMs: number;
  postprocessTimeMs: number;
  inputTokens: number;
  outputTokens: number;
}

export interface ModelDeployment {
  deploymentId: string;
  modelId: string;
  modelVersion: string;
  name: string;
  target: DeploymentTarget;
  replicas: number;
  config: DeploymentConfig;
  status: DeploymentStatus;
  endpointUrl: string;
  createdAt: string;
  updatedAt?: string;
}

export type DeploymentTarget = 'Local' | 'Cloud' | 'Edge' | 'Hybrid';
export type DeploymentStatus = 'Pending' | 'Creating' | 'Running' | 'Updating' | 'Scaling' | 'Stopped' | 'Failed';

export interface DeploymentConfig {
  minReplicas: number;
  maxReplicas: number;
  targetCpuUtilization: number;
  targetMemoryUtilization: number;
  requestTimeoutSeconds: number;
  enableGpu: boolean;
  gpuType?: string;
  environmentVariables: Record<string, string>;
}

export interface DeploymentMetrics {
  deploymentId: string;
  currentReplicas: number;
  desiredReplicas: number;
  cpuUtilization: number;
  memoryUtilization: number;
  requestsPerSecond: number;
  avgLatencyMs: number;
  errorRate: number;
  totalRequests: number;
  lastUpdated: string;
}

// Dataset types
export interface Dataset {
  datasetId: string;
  name: string;
  version: string;
  description: string;
  sourcePath: string;
  sizeBytes: number;
  sampleCount: number;
  schema: DatasetSchema;
  statistics: DatasetStatistics;
  tags: Record<string, string>;
  createdAt: string;
  createdBy: string;
}

export interface DatasetSchema {
  columns: Record<string, DatasetColumn>;
  targetColumn?: string;
}

export interface DatasetColumn {
  name: string;
  dataType: string;
  isNullable: boolean;
  statistics: Record<string, any>;
}

export interface DatasetStatistics {
  totalSamples: number;
  totalFeatures: number;
  missingValueRatio: number;
  featureStats: Record<string, Record<string, any>>;
}

// Experiment types
export interface Experiment {
  experimentId: string;
  name: string;
  description: string;
  modelId: string;
  config: ExperimentConfig;
  status: ExperimentStatus;
  results: ExperimentResults;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
}

export type ExperimentStatus = 'Created' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export interface ExperimentConfig {
  modelVersions: string[];
  datasetVersion: string;
  trainingConfig: TrainingConfig;
  parameters: Record<string, any>;
  metricsToCompare: string[];
}

export interface ExperimentResults {
  modelMetrics: Record<string, ModelMetrics>;
  bestModelVersion: string;
  comparison: Record<string, any>;
  conclusion: string;
}

// Feature Store types
export interface FeatureVector {
  entityId: string;
  features: Record<string, any>;
  timestamp: string;
}

export interface FeatureDefinition {
  name: string;
  description: string;
  dataType: string;
  source: string;
  metadata: Record<string, any>;
  createdAt: string;
}

// Monitoring types
export interface MonitoringReport {
  modelId: string;
  deploymentId: string;
  period: MonitoringPeriod;
  generatedAt: string;
  metrics: DeploymentMetrics;
  dataDrift?: DataDriftReport;
  conceptDrift?: ConceptDriftReport;
  alerts: PerformanceAlert[];
  recommendations: Recommendation[];
}

export type MonitoringPeriod = 'LastHour' | 'Last24Hours' | 'Last7Days' | 'Last30Days' | 'Custom';

export interface DataDriftReport {
  modelId: string;
  deploymentId: string;
  baselineDatasetId: string;
  detectedAt: string;
  overallDriftScore: number;
  featureDriftScores: Record<string, number>;
  driftedFeatures: Record<string, DriftDetails>;
  isSignificant: boolean;
}

export interface DriftDetails {
  driftScore: number;
  driftType: string;
  pValue: number;
  baselineStats: any;
  currentStats: any;
}

export interface ConceptDriftReport {
  modelId: string;
  deploymentId: string;
  detectedAt: string;
  driftScore: number;
  performanceDrop: number;
  detectionMethod: string;
  isSignificant: boolean;
}

export interface PerformanceAlert {
  alertId: string;
  modelId: string;
  deploymentId: string;
  severity: AlertSeverity;
  message: string;
  metricName: string;
  currentValue: number;
  threshold: number;
  triggeredAt: string;
}

export type AlertSeverity = 'Info' | 'Warning' | 'Critical';

export interface Recommendation {
  type: string;
  description: string;
  priority: number;
  parameters: Record<string, any>;
}

// Pipeline types
export interface PipelineDefinition {
  pipelineId: string;
  name: string;
  version: string;
  steps: PipelineStep[];
  parameters: Record<string, any>;
}

export interface PipelineStep {
  stepId: string;
  name: string;
  type: StepType;
  componentId: string;
  config: Record<string, any>;
  dependsOn: string[];
  retryPolicy: RetryPolicy;
}

export type StepType = 'DataIngestion' | 'DataValidation' | 'DataTransformation' | 'FeatureEngineering' | 'Training' | 'Evaluation' | 'ModelRegistration' | 'Deployment' | 'Monitoring' | 'Custom';

export interface RetryPolicy {
  maxRetries: number;
  retryDelaySeconds: number;
  exponentialBackoff: boolean;
}

export interface PipelineInput {
  parameters: Record<string, any>;
  datasetIds: string[];
  modelIds: string[];
}

export interface PipelineRun {
  runId: string;
  pipelineId: string;
  pipelineVersion: string;
  status: PipelineStatus;
  stepRuns: PipelineStepRun[];
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
  outputs: Record<string, any>;
}

export type PipelineStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export interface PipelineStepRun {
  stepId: string;
  name: string;
  status: StepStatus;
  startedAt: string;
  completedAt?: string;
  inputs: Record<string, any>;
  outputs: Record<string, any>;
  errorMessage?: string;
  retryCount: number;
}

export type StepStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Skipped' | 'Retrying';

// Health check
export interface HealthCheckResponse {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  checks: HealthCheck[];
  totalDuration: string;
}

export interface HealthCheck {
  name: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  description?: string;
  duration?: string;
}

// Pagination
export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Error
export interface ApiError {
  message: string;
  code?: string;
  details?: Record<string, string[]>;
}