import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import { api } from '../services/api';

interface License {
  id: string;
  key: string;
  name: string;
  description: string;
  tier: 'Trial' | 'Basic' | 'Pro' | 'Enterprise';
  seatLimit: number;
  features: string[];
  allowedHardwareIds: string[];
  offlineGraceDays: number;
  expiresAt?: string;
  isRevoked: boolean;
  revocationReason: string;
  issuedAt: string;
  createdAt: string;
  updatedAt: string;
  publisherId: string;
  metadata: Record<string, string>;
}

interface LicenseState {
  licenses: Record<string, License>;
  selectedLicenseId: string | null;
  filters: {
    search: string;
    tier: string | 'all';
    state: string | 'all';
  };
  isLoading: boolean;
  error: string | null;
  
  initLicenses: () => Promise<void>;
  fetchLicenses: (params?: any) => Promise<void>;
  setLicenses: (licenses: License[]) => void;
  addLicense: (license: License) => void;
  updateLicense: (id: string, updates: Partial<License>) => void;
  removeLicense: (id: string) => void;
  selectLicense: (id: string | null) => void;
  setFilters: (filters: Partial<LicenseState['filters']>) => void;
  getFilteredLicenses: () => License[];
  getSelectedLicense: () => License | null;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
}

export const useLicenseStore = create<LicenseState>()(
  persist(
    (set, get) => ({
      licenses: {},
      selectedLicenseId: null,
      filters: {
        search: '',
        tier: 'all',
        state: 'all',
      },
      isLoading: false,
      error: null,

      initLicenses: async () => {
        set({ isLoading: true, error: null });
        try {
          const response = await api.getLicenses();
          set({ licenses: Object.fromEntries(response.data.map((l: any) => [l.id, l])), isLoading: false });
        } catch (error: any) {
          set({ error: error.response?.data?.message || 'Failed to load licenses', isLoading: false });
        }
      },

      fetchLicenses: async (params) => {
        set({ isLoading: true, error: null });
        try {
          const response = await api.getLicenses(params);
          set({ licenses: Object.fromEntries(response.data.map((l: any) => [l.id, l])), isLoading: false });
        } catch (error: any) {
          set({ error: error.response?.data?.message || 'Failed to load licenses', isLoading: false });
        }
      },

      setLicenses: (licenses) =>
        set({ licenses: Object.fromEntries(licenses.map(l => [l.id, l])) }),

      addLicense: (license) =>
        set((state) => ({
          licenses: { ...state.licenses, [license.id]: license },
        })),

      updateLicense: (id, updates) =>
        set((state) => ({
          licenses: {
            ...state.licenses,
            [id]: { ...state.licenses[id], ...updates },
          },
        })),

      removeLicense: (id) =>
        set((state) => {
          const { [id]: removed, ...rest } = state.licenses;
          return { licenses: rest };
        }),

      selectLicense: (id) => set({ selectedInstanceId: id }),

      setFilters: (filters) =>
        set((state) => ({
          filters: { ...state.filters, ...filters },
        })),

      getFilteredLicenses: () => {
        const { licenses, filters } = get();
        return Object.values(licenses).filter((license) => {
          if (filters.search) {
            const search = filters.search.toLowerCase();
            if (!license.name.toLowerCase().includes(search) &&
                !license.key.toLowerCase().includes(search) &&
                !license.description?.toLowerCase().includes(search)) {
              return false;
            }
          }
          if (filters.tier !== 'all' && license.tier !== filters.tier) {
            return false;
          }
          if (filters.state !== 'all') {
            if (filters.state === 'revoked' && !license.isRevoked) return false;
            if (filters.state === 'expired' && (!license.expiresAt || new Date(license.expiresAt) > new Date())) return false;
            if (filters.state === 'active' && (license.isRevoked || (license.expiresAt && new Date(license.expiresAt) < new Date()))) return false;
          }
          return true;
        });
      },

      getSelectedLicense: () => {
        const { selectedLicenseId, licenses } = get();
        return selectedLicenseId ? licenses[selectedLicenseId] : null;
      },

      setLoading: (loading: boolean) => set({ isLoading: loading }),
      setError: (error: string | null) => set({ error }),
    }),
    {
      name: 'license-storage',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        selectedLicenseId: state.selectedLicenseId,
        filters: state.filters,
      }),
    }
  )
);

export const useLicenses = () => useLicenseStore();
