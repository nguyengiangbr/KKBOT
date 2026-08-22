import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { Alert } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { api } from '../services/api';
import { User, AuthTokens, LoginRequest, RegisterRequest } from '../types/auth';

interface AuthContextType {
  user: User | null;
  tokens: AuthTokens | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
  refreshToken: () => Promise<void>;
  updateUser: (user: Partial<User>) => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

const STORAGE_KEYS = {
  TOKENS: '@kkbot_auth_tokens',
  USER: '@kkbot_user',
  REFRESH_TOKEN: '@kkbot_refresh_token',
};

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [tokens, setTokens] = useState<AuthTokens | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const isAuthenticated = !!tokens?.accessToken;

  const loadStoredAuth = useCallback(async () => {
    try {
      const [storedTokens, storedUser] = await Promise.all([
        AsyncStorage.getItem(STORAGE_KEYS.TOKENS),
        AsyncStorage.getItem(STORAGE_KEYS.USER),
      ]);

      if (storedTokens) {
        const parsedTokens = JSON.parse(storedTokens);
        setTokens(parsedTokens);
        api.defaults.headers.common['Authorization'] = `Bearer ${parsedTokens.accessToken}`;
      }

      if (storedUser) {
        setUser(JSON.parse(storedUser));
      }
    } catch (error) {
      console.error('Failed to load stored auth:', error);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadStoredAuth();
  }, [loadStoredAuth]);

  const storeAuth = async (newTokens: AuthTokens, newUser: User) => {
    try {
      await Promise.all([
        AsyncStorage.setItem(STORAGE_KEYS.TOKENS, JSON.stringify(newTokens)),
        AsyncStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(newUser)),
      ]);
      setTokens(newTokens);
      setUser(newUser);
      api.defaults.headers.common['Authorization'] = `Bearer ${newTokens.accessToken}`;
    } catch (error) {
      console.error('Failed to store auth:', error);
      throw error;
    }
  };

  const clearAuth = async () => {
    try {
      await Promise.all([
        AsyncStorage.removeItem(STORAGE_KEYS.TOKENS),
        AsyncStorage.removeItem(STORAGE_KEYS.USER),
        AsyncStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN),
      ]);
      setTokens(null);
      setUser(null);
      delete api.defaults.headers.common['Authorization'];
    } catch (error) {
      console.error('Failed to clear auth:', error);
    }
  };

  const login = async (credentials: LoginRequest) => {
    try {
      const response = await api.post('/auth/login', credentials);
      const { accessToken, expiresIn, tokenType, user } = response.data;
      
      const tokens: AuthTokens = {
        accessToken,
        expiresIn,
        tokenType,
        refreshToken: response.data.refreshToken,
      };

      await storeAuth(tokens, user);
    } catch (error: any) {
      const message = error.response?.data?.error || error.message || 'Login failed';
      Alert.alert('Login Failed', message);
      throw error;
    }
  };

  const register = async (data: RegisterRequest) => {
    try {
      const response = await api.post('/auth/register', data);
      const { accessToken, expiresIn, tokenType, user } = response.data;
      
      const tokens: AuthTokens = {
        accessToken,
        expiresIn,
        tokenType,
        refreshToken: response.data.refreshToken,
      };

      await storeAuth(tokens, user);
    } catch (error: any) {
      const message = error.response?.data?.error || error.message || 'Registration failed';
      Alert.alert('Registration Failed', message);
      throw error;
    }
  };

  const logout = async () => {
    try {
      await api.post('/auth/logout');
    } catch (error) {
      console.error('Logout API error:', error);
    } finally {
      await clearAuth();
    }
  };

  const refreshToken = async () => {
    try {
      const storedRefreshToken = await AsyncStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN);
      if (!storedRefreshToken) throw new Error('No refresh token');

      const response = await api.post('/auth/refresh', { refreshToken: storedRefreshToken });
      const { accessToken, expiresIn, tokenType } = response.data;

      const newTokens: AuthTokens = {
        ...tokens!,
        accessToken,
        expiresIn,
        tokenType,
      };

      await AsyncStorage.setItem(STORAGE_KEYS.TOKENS, JSON.stringify(newTokens));
      setTokens(newTokens);
      api.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`;
    } catch (error) {
      await clearAuth();
      throw error;
    }
  };

  const updateUser = (userData: Partial<User>) => {
    setUser(prev => prev ? { ...prev, ...userData } : null);
    if (user) {
      AsyncStorage.setItem(STORAGE_KEYS.USER, JSON.stringify({ ...user, ...userData }));
    }
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        tokens,
        isLoading,
        isAuthenticated,
        login,
        register,
        logout,
        refreshToken,
        updateUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};