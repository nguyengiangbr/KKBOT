import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import { api } from '../services/api';

interface Publisher {
  id: string;
  name: string;
  email: string;
  publicKey: string;
  isVerified: boolean;
  revenueSharePercent: number;
  createdAt: string;
  licenseCount: number;
}

interface PublisherState {
  publishers: Record<string, Publisher>;
  selectedPublisherId: string | null;
  filters: {
    search: string;
    verifiedOnly: boolean;
  };
  isLoading: boolean;
  error: string | null;
  
  initPublishers: () => Promise<void>;
  fetchPublishers: (params?: any) => Promise<void>;
  setPublishers: (publishers: Publisher[]) => void;
  addPublisher: (publisher: Publisher) => void;
  updatePublisher: (id: string, updates: Partial<Publisher>) => void;
  removePublisher: (id: string) => void;
  selectPublisher: (id: string | null) => void;
  setFilters: (filters: Partial<PublisherState['filters']>) => void;
  getFilteredPublishers: () => Publisher[];
  getSelectedPublisher: () => Publisher | null;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
}

export const usePublisherStore = create<PublisherState>()(
  persist(
    (set, get) => ({
      publishers: {},
      selectedPublisherId: null,
      filters: {
        search: '',
        verifiedOnly: false,
      },
      isLoading: false,
      error: null,

      initPublishers: async () => {
        set({ isLoading: true, error: null });
        try {
          const response = await api.getPublishers();
          set({ publishers: Object.fromEntries(response.data.map((p: any) => [p.id, p])), isLoading: false });
        } catch (error: any) {
          set({ error: error.response?.data?.message || 'Failed to load publishers', isLoading: false });
        }
      },

      fetchPublishers: async (params) => {
        set({ isLoading: true, error: null });
        try {
          const response = await api.getPublishers(params);
          set({ publishers: Object.fromEntries(response.data.map((p: any) => [p.id, p])), isLoading: false });
        } catch (error: any) {
          set({ error: error.response?.data?.message || 'Failed to load publishers', isLoading: false });
        }
      },

      setPublishers: (publishers) =>
        set({ publishers: Object.fromEntries(publishers.map(p => [p.id, p])) }),

      addPublisher: (publisher) =>
        set((state) => ({
          publishers: { ...state.publishers, [publisher.id]: publisher },
        })),

      updatePublisher: (id, updates) =>
        set((state) => ({
          publishers: {
            ...state.publishers,
            [id]: { ...state.publishers[id], ...updates },
          },
        })),

      removePublisher: (id) =>
        set((state) => {
          const { [id]: removed, ...rest } = state.publishers;
          return { publishers: rest };
        }),

      selectPublisher: (id) => set({ selectedPublisherId: id }),

      setFilters: (filters) =>
        set((state) => ({
          filters: { ...state.filters, ...filters },
        })),

      getFilteredPublishers: () => {
        const { publishers, filters } = get();
        return Object.values(publishers).filter((publisher) => {
          if (filters.search) {
            const search = filters.search.toLowerCase();
            if (!publisher.name.toLowerCase().includes(search) &&
                !publisher.email.toLowerCase().includes(search)) {
              return false;
            }
          }
          if (filters.verifiedOnly && !publisher.isVerified) {
            return false;
          }
          return true;
        });
      },

      getSelectedPublisher: () => {
        const { selectedPublisherId, publishers } = get();
        return selectedPublisherId ? publishers[selectedPublisherId] : null;
      },

      setLoading: (loading: boolean) => set({ isLoading: loading }),
      setError: (error: string | null) => set({ error }),
    }),
    {
      name: 'publisher-storage',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        selectedPublisherId: state.selectedPublisherId,
        filters: state.filters,
      }),
    }
  )
);

export const usePublishers = () => usePublisherStore();
