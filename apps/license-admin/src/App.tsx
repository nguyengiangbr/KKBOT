import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { Toaster } from 'react-hot-toast';
import { AppRoutes } from './routes';
import { theme } from './theme';
import { useAuthStore } from './stores/authStore';
import { useLicenseStore } from './stores/licenseStore';
import { socket } from './services/socket';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30000,
      cacheTime: 300000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

function App() {
  const initAuth = useAuthStore(state => state.initAuth);
  const initLicenses = useLicenseStore(state => state.initLicenses);

  React.useEffect(() => {
    initAuth();
    initLicenses();
    
    const unsubscribe = useAuthStore.subscribe(
      (state) => state.isAuthenticated,
      (isAuthenticated) => {
        if (isAuthenticated) {
          socket.connect();
        } else {
          socket.disconnect();
        }
      }
    );
    
    return unsubscribe;
  }, [initAuth, initLicenses]);

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <BrowserRouter>
          <AppRoutes />
          <Toaster
            position="top-right"
            toastOptions={{
              duration: 4000,
              style: {
                background: '#1E293B',
                color: '#F8FAFC',
                borderRadius: '12px',
                padding: '16px',
              },
              success: {
                iconTheme: {
                  primary: '#10B981',
                  secondary: '#FFFFFF',
                },
              },
              error: {
                iconTheme: {
                  primary: '#EF4444',
                  secondary: '#FFFFFF',
                },
              },
            }}
          />
        </BrowserRouter>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

export default App;
