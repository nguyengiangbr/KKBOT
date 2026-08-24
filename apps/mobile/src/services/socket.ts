import { io, Socket } from 'socket.io-client';
import { storage } from '../App';

class SocketService {
  private socket: Socket | null = null;
  private listeners: Map<string, Set<Function>> = new Map();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;

  connect() {
    if (this.socket?.connected) return;

    const token = storage.getString('auth_token');
    const wsUrl = __DEV__ 
      ? 'http://10.0.2.2:5000' 
      : 'wss://api.kkbot.io';

    this.socket = io(wsUrl, {
      auth: { token },
      transports: ['websocket', 'polling'],
      reconnection: true,
      reconnectionAttempts: this.maxReconnectAttempts,
      reconnectionDelay: 1000,
      reconnectionDelayMax: 5000,
      timeout: 10000,
    });

    this.socket.on('connect', () => {
      console.log('Socket connected');
      this.reconnectAttempts = 0;
      this.emit('connected', true);
    });

    this.socket.on('disconnect', (reason) => {
      console.log('Socket disconnected:', reason);
      this.emit('connected', false);
    });

    this.socket.on('connect_error', (error) => {
      console.error('Socket connection error:', error);
      this.reconnectAttempts++;
      if (this.reconnectAttempts >= this.maxReconnectAttempts) {
        this.emit('connection_failed', error);
      }
    });

    // Instance events
    this.socket.on('instance:state_changed', (data) => {
      this.emit('instance:state_changed', data);
    });

    this.socket.on('instance:metrics', (data) => {
      this.emit('instance:metrics', data);
    });

    this.socket.on('instance:log', (data) => {
      this.emit('instance:log', data);
    });

    // Plugin events
    this.socket.on('plugin:loaded', (data) => {
      this.emit('plugin:loaded', data);
    });

    this.socket.on('plugin:unloaded', (data) => {
      this.emit('plugin:unloaded', data);
    });

    // Scenario events
    this.socket.on('scenario:started', (data) => {
      this.emit('scenario:started', data);
    });

    this.socket.on('scenario:completed', (data) => {
      this.emit('scenario:completed', data);
    });

    // System events
    this.socket.on('system:alert', (data) => {
      this.emit('system:alert', data);
    });

    this.socket.on('system:metrics', (data) => {
      this.emit('system:metrics', data);
    });
  }

  disconnect() {
    if (this.socket) {
      this.socket.disconnect();
      this.socket = null;
    }
  }

  on(event: string, callback: Function) {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, new Set());
    }
    this.listeners.get(event)!.add(callback);

    // Return unsubscribe function
    return () => {
      this.listeners.get(event)?.delete(callback);
    };
  }

  off(event: string, callback: Function) {
    this.listeners.get(event)?.delete(callback);
  }

  private emit(event: string, data: any) {
    this.listeners.get(event)?.forEach(callback => {
      try {
        callback(data);
      } catch (error) {
        console.error(`Error in socket listener for ${event}:`, error);
      }
    });
  }

  // Send events
  subscribeToInstance(instanceId: string) {
    this.socket?.emit('subscribe:instance', instanceId);
  }

  unsubscribeFromInstance(instanceId: string) {
    this.socket?.emit('unsubscribe:instance', instanceId);
  }

  sendCommand(instanceId: string, command: string, params?: any) {
    this.socket?.emit('instance:command', { instanceId, command, params });
  }

  get connected() {
    return this.socket?.connected ?? false;
  }
}

export const socket = new SocketService();
