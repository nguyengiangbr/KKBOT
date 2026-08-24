import axios, { AxiosInstance, AxiosError, InternalAxiosRequestConfig } from 'axios';
import { storage } from '../App';

const API_BASE_URL = __DEV__ 
  ? 'http://10.0.2.2:5000/api'  // Android emulator localhost
  : 'https://api.kkbot.io/api';

class ApiService {
  private client: AxiosInstance;

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    this.setupInterceptors();
  }

  private setupInterceptors() {
    // Request interceptor - add auth token
    this.client.interceptors.request.use(
      async (config: InternalAxiosRequestConfig) => {
        const token = storage.getString('auth_token');
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor - handle errors, refresh token
    this.client.interceptors.response.use(
      (response) => response,
      async (error: AxiosError) => {
        const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

        if (error.response?.status === 401 && !originalRequest._retry) {
          originalRequest._retry = true;
          
          try {
            const refreshToken = storage.getString('refresh_token');
            if (refreshToken) {
              const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
                refreshToken,
              });
              
              const { accessToken, refreshToken: newRefreshToken } = response.data;
              storage.set('auth_token', accessToken);
              storage.set('refresh_token', newRefreshToken);
              
              originalRequest.headers.Authorization = `Bearer ${accessToken}`;
              return this.client(originalRequest);
            }
          } catch (refreshError) {
            // Refresh failed, clear auth and redirect to login
            storage.delete('auth_token');
            storage.delete('refresh_token');
            // Navigation will handle redirect
          }
        }

        return Promise.reject(error);
      }
    );
  }

  // Auth
  async login(username: string, password: string) {
    const response = await this.client.post('/auth/login', { username, password });
    const { accessToken, refreshToken } = response.data;
    storage.set('auth_token', accessToken);
    storage.set('refresh_token', refreshToken);
    return response.data;
  }

  async logout() {
    storage.delete('auth_token');
    storage.delete('refresh_token');
    return this.client.post('/auth/logout');
  }

  async getProfile() {
    const response = await this.client.get('/auth/profile');
    return response.data;
  }

  // Instances
  async getInstances(params?: { search?: string; state?: string; page?: number; limit?: number }) {
    const response = await this.client.get('/instances', { params });
    return response.data;
  }

  async getInstance(id: string) {
    const response = await this.client.get(`/instances/${id}`);
    return response.data;
  }

  async createInstance(data: any) {
    const response = await this.client.post('/instances', data);
    return response.data;
  }

  async startInstance(id: string) {
    const response = await this.client.post(`/instances/${id}/start`);
    return response.data;
  }

  async stopInstance(id: string, reason?: string) {
    const response = await this.client.post(`/instances/${id}/stop`, { reason });
    return response.data;
  }

  async restartInstance(id: string) {
    const response = await this.client.post(`/instances/${id}/restart`);
    return response.data;
  }

  async deleteInstance(id: string) {
    const response = await this.client.delete(`/instances/${id}`);
    return response.data;
  }

  // Plugins
  async getPlugins(params?: { category?: string; search?: string }) {
    const response = await this.client.get('/plugins', { params });
    return response.data;
  }

  async getPlugin(id: string) {
    const response = await this.client.get(`/plugins/${id}`);
    return response.data;
  }

  async installPlugin(id: string, version?: string) {
    const response = await this.client.post(`/plugins/${id}/install`, { version });
    return response.data;
  }

  async uninstallPlugin(id: string) {
    const response = await this.client.delete(`/plugins/${id}/install`);
    return response.data;
  }

  // Scenarios
  async getScenarios(params?: { search?: string }) {
    const response = await this.client.get('/scenarios', { params });
    return response.data;
  }

  async getScenario(id: string) {
    const response = await this.client.get(`/scenarios/${id}`);
    return response.data;
  }

  async runScenario(id: string, deviceId?: string) {
    const response = await this.client.post(`/scenarios/${id}/run`, { deviceId });
    return response.data;
  }

  // Devices
  async getDevices() {
    const response = await this.client.get('/devices');
    return response.data;
  }

  async getDevice(id: string) {
    const response = await this.client.get(`/devices/${id}`);
    return response.data;
  }

  // Dashboard
  async getDashboardStats() {
    const response = await this.client.get('/dashboard/stats');
    return response.data;
  }

  async getRecentActivity(params?: { limit?: number }) {
    const response = await this.client.get('/dashboard/activity', { params });
    return response.data;
  }

  // Metrics
  async getMetrics(instanceId?: string, timeRange?: string) {
    const response = await this.client.get('/metrics', { params: { instanceId, timeRange } });
    return response.data;
  }
}

export const api = new ApiService();
