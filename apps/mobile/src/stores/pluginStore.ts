import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { zustandStorage } from '../App';

interface Plugin {
  id: string;
  name: string;
  description: string;
  version: string;
  category: string;
  tags: string[];
  author: string;
  priceType: 'Free' | 'Paid' | 'Subscription' | 'Freemium';
  price: number;
  rating: number;
  downloadCount: number;
  publisherVerified: boolean;
  installed: boolean;
  installedVersion?: string;
  latestVersion: string;
  description: string;
  changelog: string;
  readme: string;
  screenshots: string[];
  dependencies: { pluginId: string; version: string }[];
}

interface PluginState {
  plugins: Record<string, Plugin>;
  installedPlugins: string[];
  filters: {
    search: string;
    category: string | 'all';
    priceType: string | 'all';
    verifiedOnly: boolean;
    installedOnly: boolean;
  };
  sortBy: 'rating' | 'downloads' | 'updated' | 'name';
  
  setPlugins: (plugins: Plugin[]) => void;
  addPlugin: (plugin: Plugin) => void;
  updatePlugin: (id: string, updates: Partial<Plugin>) => void;
  installPlugin: (id: string, version: string) => void;
  uninstallPlugin: (id: string) => void;
  setFilters: (filters: Partial<PluginState['filters']>) => void;
  setSortBy: (sortBy: PluginState['sortBy']) => void;
  getFilteredPlugins: () => Plugin[];
  getInstalledPlugins: () => Plugin[];
}

export const usePluginStore = create<PluginState>()(
  persist(
    (set, get) => ({
      plugins: {},
      installedPlugins: [],
      filters: {
        search: '',
        category: 'all',
        priceType: 'all',
        verifiedOnly: false,
        installedOnly: false,
      },
      sortBy: 'rating',

      setPlugins: (plugins) =>
        set({ plugins: Object.fromEntries(plugins.map(p => [p.id, p])) }),

      addPlugin: (plugin) =>
        set((state) => ({
          plugins: { ...state.plugins, [plugin.id]: plugin },
        })),

      updatePlugin: (id, updates) =>
        set((state) => ({
          plugins: {
            ...state.plugins,
            [id]: { ...state.plugins[id], ...updates },
          },
        })),

      installPlugin: (id, version) =>
        set((state) => ({
          plugins: {
            ...state.plugins,
            [id]: {
              ...state.plugins[id],
              installed: true,
              installedVersion: version,
            },
          },
          installedPlugins: [...state.installedPlugins, id],
        })),

      uninstallPlugin: (id) =>
        set((state) => ({
          plugins: {
            ...state.plugins,
            [id]: { ...state.plugins[id], installed: false, installedVersion: undefined },
          },
          installedPlugins: state.installedPlugins.filter(p => p !== id),
        })),

      setFilters: (filters) =>
        set((state) => ({
          filters: { ...state.filters, ...filters },
        })),

      setSortBy: (sortBy) => set({ sortBy }),

      getFilteredPlugins: () => {
        const { plugins, filters, sortBy } = get();
        let filtered = Object.values(plugins).filter((plugin) => {
          if (filters.search) {
            const search = filters.search.toLowerCase();
            if (!plugin.name.toLowerCase().includes(search) &&
                !plugin.description.toLowerCase().includes(search) &&
                !plugin.tags.some(t => t.toLowerCase().includes(search))) {
              return false;
            }
          }
          if (filters.category !== 'all' && plugin.category !== filters.category) {
            return false;
          }
          if (filters.priceType !== 'all' && plugin.priceType !== filters.priceType) {
            return false;
          }
          if (filters.verifiedOnly && !plugin.publisherVerified) {
            return false;
          }
          if (filters.installedOnly && !plugin.installed) {
            return false;
          }
          return true;
        });

        // Sort
        filtered.sort((a, b) => {
          switch (sortBy) {
            case 'rating':
              return b.rating - a.rating;
            case 'downloads':
              return b.downloadCount - a.downloadCount;
            case 'updated':
              return new Date(b.latestVersion).getTime() - new Date(a.latestVersion).getTime();
            case 'name':
              return a.name.localeCompare(b.name);
            default:
              return 0;
          }
        });

        return filtered;
      },

      getInstalledPlugins: () => {
        const { plugins, installedPlugins } = get();
        return installedPlugins.map(id => plugins[id]).filter(Boolean);
      },
    }),
    {
      name: 'plugin-storage',
      storage: zustandStorage,
      partialize: (state) => ({
        installedPlugins: state.installedPlugins,
        filters: state.filters,
        sortBy: state.sortBy,
      }),
    }
  )
);

export const usePlugins = () => usePluginStore();
