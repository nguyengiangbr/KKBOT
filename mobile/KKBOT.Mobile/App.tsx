import React from 'react';
import { Provider as PaperProvider } from 'react-native-paper';
import { NavigationContainer } from '@react-navigation/native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PersistClient } from '@tanstack/react-query-persist-client';
import { createSyncStoragePersister } from '@tanstack/query-sync-storage-persister';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { AppRegistry } from 'react-native';
import { AppNavigator } from './src/navigation/AppNavigator';
import { AuthProvider } from './src/context/AuthContext';
import { ThemeProvider } from './src/context/ThemeContext';
import { NotificationProvider } from './src/context/NotificationContext';
import { theme } from './src/theme/theme';
import { errorBoundary } from './src/utils/errorBoundary';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30000,
      cacheTime: 300000,
      retry: 2,
      refetchOnWindowFocus: false,
    },
  },
});

const persister = createSyncStoragePersister({
  storage: AsyncStorage,
});

const App = () => {
  return (
    <QueryClientProvider client={queryClient}>
      <PersistClient persistOptions={{ persister, maxAge: 1000 * 60 * 60 * 24 }}>
        <PaperProvider theme={theme}>
          <SafeAreaProvider>
            <ThemeProvider>
              <AuthProvider>
                <NotificationProvider>
                  <NavigationContainer>
                    <AppNavigator />
                  </NavigationContainer>
                </NotificationProvider>
              </AuthProvider>
            </ThemeProvider>
          </SafeAreaProvider>
        </PaperProvider>
      </PersistClient>
    </QueryClientProvider>
  );
};

export default errorBoundary(App);

AppRegistry.registerComponent('KKBOTMobile', () => App);