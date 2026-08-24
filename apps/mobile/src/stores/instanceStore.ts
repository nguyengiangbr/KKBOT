import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { zustandStorage } from '../App';

interface Instance {
  id: string;
  displayName: string;
  deviceId: string;
  moduleId: string;
  module?: { id: string; name: string };
  state: 'Running' | 'Stopped' | 'Starting' | 'Stopping' | 'Error' | 'Idle';
  version: string;
  deviceType: string;
  config: Record<string, any>;
  metrics?: {
    cpuUsage: number;
    memoryUsage: number;
    fps: number;
    uptime: number;
  };
  createdAt: string;
  updatedAt: string;
}

interface InstanceState {
  instances: Record<string, Instance>;
  selectedInstanceId: string | null;
  filters: {
    search: string;
    state: string | 'all';
    module: string | 'all';
  };
  
  setInstances: (instances: Instance[]) => void;
  addInstance: (instance: Instance) => void;
  updateInstance: (id: string, updates: Partial<Instance>) => void;
  removeInstance: (id: string) => void;
  selectInstance: (id: string | null) => void;
  setFilters: (filters: Partial<InstanceState['filters']>) => void;
  getFilteredInstances: () => Instance[];
  getSelectedInstance: () => Instance | null;
}

export const useInstanceStore = create<InstanceState>()(
  persist(
    (set, get) => ({
      instances: {},
      selectedInstanceId: null,
      filters: {
        search: '',
        state: 'all',
        module: 'all',
      },

      setInstances: (instances) =>
        set({ instances: Object.fromEntries(instances.map(i => [i.id, i])) }),

      addInstance: (instance) =>
        set((state) => ({
          instances: { ...state.instances, [instance.id]: instance },
        })),

      updateInstance: (id, updates) =>
        set((state) => ({
          instances: {
            ...state.instances,
            [id]: { ...state.instances[id], ...updates },
          },
        })),

      removeInstance: (id) =>
        set((state) => {
          const { [id]: removed, ...rest } = state.instances;
          return { instances: rest };
        }),

      selectInstance: (id) => set({ selectedInstanceId: id }),

      setFilters: (filters) =>
        set((state) => ({
          filters: { ...state.filters, ...filters },
        })),

      getFilteredInstances: () => {
        const { instances, filters } = get();
        return Object.values(instances).filter((instance) => {
          if (filters.search) {
            const search = filters.search.toLowerCase();
            if (!instance.displayName.toLowerCase().includes(search) &&
                !instance.deviceId.toLowerCase().includes(search) &&
                !instance.module?.name?.toLowerCase().includes(search)) {
              return false;
            }
          }
          if (filters.state !== 'all' && instance.state !== filters.state) {
            return false;
          }
          if (filters.module !== 'all' && instance.moduleId !== filters.module) {
            return false;
          }
          return true;
        });
      },

      getSelectedInstance: () => {
        const { selectedInstanceId, instances } = get();
        return selectedInstanceId ? instances[selectedInstanceId] : null;
      },
    }),
    {
      name: 'instance-storage',
      storage: zustandStorage,
      partialize: (state) => ({
        selectedInstanceId: state.selectedInstanceId,
        filters: state.filters,
      }),
    }
  )
);

export const useInstances = () => useInstanceStore();
