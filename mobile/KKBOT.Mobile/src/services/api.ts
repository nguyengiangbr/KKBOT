import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';

const API_BASE_URL = __DEV__ 
  ? (Platform.OS === 'android' ? 'http://10.0.2.2:8080/api/v1' : 'http://localhost:8080/api/v1')
  : 'https://api.kkbot.example.com/api/v1';

const STORAGE_KEY = '@kkbot_auth_tokens';

class ApiService {
  private instance: AxiosInstance;

  constructor() {
    this.instance = axios.create({
      baseURL: API_BASE_URL,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
    });

    this.instance.interceptors.request.use(
      async (config: InternalAxiosRequestConfig) => {
        try {
          const stored = await AsyncStorage.getItem('@kkbot_auth_tokens');
          if (stored) {
            const { accessToken } = JSON.parse(stored);
            if (accessToken) {
              config.headers.Authorization = `Bearer ${accessToken}`;
            }
          }
        } catch (error) {
          console.error('Failed to get auth token:', error);
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    this.instance.interceptors.response.use(
      (response) => response,
      async (error: AxiosError) => {
        const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

        if (error.response?.status === 401 && !originalRequest._retry) {
          originalRequest._retry = true;

          try {
            const refreshToken = await this.getRefreshToken();
            if (refreshToken) {
              const { data } = await this.instance.post('/auth/refresh', {
                refreshToken,
              });

              const { accessToken, expiresIn, tokenType } = data;
              await this.storeTokens({
                accessToken,
                expiresIn,
                tokenType: data.tokenType,
                refreshToken: data.refreshToken,
              });

              originalRequest.headers.Authorization = `Bearer ${accessToken}`;
              return this.instance(originalRequest);
            }
          } catch (refreshError) {
            await this.clearTokens();
            // Navigate to login would be handled by auth context
          }
        }

        return Promise.reject(error);
      }
    );
  }

  private async getRefreshToken(): Promise<string | null> {
    try {
      const stored = await AsyncStorage.getItem('@kkbot_auth_tokens');
      if (stored) {
        const { refreshToken } = JSON.parse(stored);
        return refreshToken;
      }
    } catch (error) {
      console.error('Failed to get refresh token:', error);
    }
    return null;
  }

  private async storeTokens(tokens: { accessToken: string; expiresIn: number; tokenType: string; refreshToken?: string }): Promise<void> {
    try {
      await AsyncStorage.setItem('@kkbot_auth_tokens', JSON.stringify(tokens));
    } catch (error) {
      console.error('Failed to store tokens:', error);
    }
  }

  private async clearTokens(): Promise<void> {
    try {
      await AsyncStorage.removeItem('@kkbot_auth_tokens');
    } catch (error) {
      console.error('Failed to clear tokens:', error);
    }
  }

  // Auth
  login = (data: { email: string; password: string; rememberMe?: boolean }) =>
    this.instance.post('/auth/login', data);

  register = (data: { email: string; password: string; firstName: string; lastName: string }) =>
    this.instance.post('/auth/register', data);

  logout = () => this.instance.post('/auth/logout');

  refreshToken = (refreshToken: string) =>
    this.instance.post('/auth/refresh', { refreshToken });

  getCurrentUser = () => this.instance.get('/auth/me');

  changePassword = (data: { currentPassword: string; newPassword: string }) =>
    this.instance.post('/auth/change-password', data);

  // Instances
  getInstances = (params?: { page?: number; pageSize?: number; status?: string }) =>
    this.instance.get('/instances', { params });

  getInstance = (id: string) => this.instance.get(`/instances/${id}`);

  createInstance = (data: { name: string; deviceId?: string; moduleId?: string; configuration?: string }) =>
    this.instance.post('/instances', data);

  updateInstance = (id: string, data: Partial<{ name: string; status: string; deviceId: string; moduleId: string; configuration: string }>) =>
    this.instance.put(`/instances/${id}`, data);

  deleteInstance = (id: string) => this.instance.delete(`/instances/${id}`);

  startInstance = (id: string) => this.instance.post(`/instances/${id}/start`);

  stopInstance = (id: string) => this.instance.post(`/instances/${id}/stop`);

  // Jobs
  getJobs = (params?: { instanceId?: string; page?: number; pageSize?: number; status?: string; type?: string }) =>
    this.instance.get('/jobs', { params });

  getJob = (id: string) => this.instance.get(`/jobs/${id}`);

  createJob = (data: { instanceId: string; type: string; payload?: string; priority?: number; scheduledAt?: string }) =>
    this.instance.post('/jobs', data);

  updateJob = (id: string, data: Partial<{ status: string; priority: number; payload: string; result: string; errorMessage: string }>) =>
    this.instance.put(`/jobs/${id}`, data);

  deleteJob = (id: string) => this.instance.delete(`/jobs/${id}`);

  // Plugins
  getPlugins = (params?: { path?: string }) => this.instance.get('/plugins', { params });

  reloadPlugins = (path?: string) => this.instance.post('/plugins/reload', null, { params: { path } });

  // Marketplace
  searchMarketplace = (params: { keyword?: string; tags?: string[]; author?: string; verifiedOnly?: boolean; page?: number; pageSize?: number; sortBy?: string; sortDescending?: boolean }) =>
    this.instance.get('/marketplace/search', { params });

  getMarketplacePlugin = (pluginId: string, version?: string) =>
    this.instance.get(`/marketplace/plugins/${pluginId}`, { params: { version } });

  installPlugin = (pluginId: string, version?: string) =>
    this.instance.post(`/marketplace/install/${pluginId}`, null, { params: { version } });

  updatePlugin = (pluginId: string) =>
    this.instance.post(`/marketplace/update/${pluginId}`);

  uninstallPlugin = (pluginId: string) =>
    this.instance.delete(`/marketplace/uninstall/${pluginId}`);

  getInstalledPlugins = () => this.instance.get('/marketplace/installed');

  // AI Models
  getModels = (params?: { type?: string; framework?: string; status?: string; tags?: string[]; author?: string; page?: number; pageSize?: number; sortBy?: string; sortDescending?: boolean }) =>
    this.instance.get('/ai/models', { params });

  getModel = (modelId: string, version?: string) =>
    this.instance.get(`/ai/models/${modelId}`, { params: { version } });

  registerModel = (model: any) => this.instance.post('/ai/models', model);

  updateModel = (modelId: string, version: string, model: any) =>
    this.instance.put(`/ai/models/${modelId}/${version}`, model);

  deleteModel = (modelId: string, version: string) =>
    this.instance.delete(`/ai/models/${modelId}/${version}`);

  startTraining = (request: any) => this.instance.post('/ai/models/training', request);

  getTrainingJob = (jobId: string) => this.instance.get(`/ai/models/training/${jobId}`);

  listTrainingJobs = (params?: { modelId?: string; status?: string; fromDate?: string; toDate?: string; page?: number; pageSize?: number }) =>
    this.instance.get('/ai/models/training', { params });

  cancelTraining = (jobId: string) => this.instance.post(`/ai/models/training/${jobId}/cancel`);

  predict = (request: any) => this.instance.post('/ai/models/predict', request);

  predictBatch = (requests: any[]) => this.instance.post('/ai/models/predict/batch', requests);

  deployModel = (request: any) => this.instance.post('/ai/models/deployments', request);

  getDeployment = (deploymentId: string) => this.instance.get(`/ai/models/deployments/${deploymentId}`);

  listDeployments = (params?: { modelId?: string; target?: string; status?: string; page?: number; pageSize?: number }) =>
    this.instance.get('/ai/models/deployments', { params });

  scaleDeployment = (deploymentId: string, replicas: number) =>
    this.instance.post(`/ai/models/deployments/${deploymentId}/scale`, { replicas });

  getDeploymentMetrics = (deploymentId: string) =>
    this.instance.get(`/ai/models/deployments/${deploymentId}/metrics`);

  // Datasets
  getDatasets = (params?: { name?: string; tags?: string[]; page?: number; pageSize?: number }) =>
    this.instance.get('/ai/datasets', { params });

  getDataset = (datasetId: string, version?: string) =>
    this.instance.get(`/ai/datasets/${datasetId}`, { params: { version } });

  registerDataset = (request: any) => this.instance.post('/ai/datasets', request);

  validateDataset = (datasetId: string, version: string) =>
    this.instance.get(`/ai/datasets/${datasetId}/${version}/validate`);

  getDatasetStatistics = (datasetId: string, version: string) =>
    this.instance.get(`/ai/datasets/${datasetId}/${version}/statistics`);

  deleteDataset = (datasetId: string, version: string) =>
    this.instance.delete(`/ai/datasets/${datasetId}/${version}`);

  // Experiments
  getExperiments = (params?: { modelId?: string; status?: string; fromDate?: string; toDate?: string; page?: number; pageSize?: number }) =>
    this.instance.get('/ai/experiments', { params });

  getExperiment = (experimentId: string) => this.instance.get(`/ai/experiments/${experimentId}`);

  createExperiment = (request: any) => this.instance.post('/ai/experiments', request);

  runExperiment = (experimentId: string) => this.instance.post(`/ai/experiments/${experimentId}/run`);

  compareExperiments = (experimentIds: string[]) =>
    this.instance.post('/ai/experiments/compare', { experimentIds });

  // Pipelines
  runPipeline = (pipeline: any, input: any) => this.instance.post('/ai/pipelines/run', { pipeline, input });

  getPipelineRun = (runId: string) => this.instance.get(`/ai/pipelines/runs/${runId}`);

  listPipelineRuns = (params?: { pipelineId?: string; status?: string; fromDate?: string; toDate?: string; page?: number; pageSize?: number }) =>
    this.instance.get('/ai/pipelines/runs', { params });

  cancelPipelineRun = (runId: string) => this.instance.post(`/ai/pipelines/runs/${runId}/cancel`);

  // Users
  getUsers = (tenantId?: string) => this.instance.get('/users', { params: { tenantId } });

  getUser = (id: string) => this.instance.get(`/users/${id}`);

  updateUser = (id: string, data: any) => this.instance.put(`/users/${id}`, data);

  deleteUser = (id: string) => this.instance.delete(`/users/${id}`);

  // Tenants
  getTenants = () => this.instance.get('/tenants');

  getTenant = (id: string) => this.instance.get(`/tenants/${id}`);

  createTenant = (data: any) => this.instance.post('/tenants', data);

  updateTenant = (id: string, data: any) => this.instance.put(`/tenants/${id}`, data);

  deleteTenant = (id: string) => this.instance.delete(`/tenants/${id}`);

  // Health
  healthCheck = () => this.instance.get('/health');

  // Generic methods
  get = (url: string, config?: any) => this.instance.get(url, config);
  post = (url: string, data?: any, config?: any) => this.instance.post(url, data, config);
  put = (url: string, data?: any, config?: any) => this.instance.put(url, data, config);
  patch = (url: string, data?: any, config?: any) => this.instance.patch(url, data, config);
  delete = (url: string, config?: any) => this.instance.delete(url, config);
}

export const api = new ApiService();
export default api;