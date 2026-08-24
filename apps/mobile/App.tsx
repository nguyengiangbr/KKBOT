import React from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createStackNavigator } from '@react-navigation/stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Provider as PaperProvider } from 'react-native-paper';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { Reanimated } from 'react-native-reanimated';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toast } from 'react-native-toast-message';
import { MMKV } from 'react-native-mmkv';
import { create } from 'zustand';
import { PersistStorage } from 'zustand/middleware';

// Screens
import { DashboardScreen } from './src/screens/DashboardScreen';
import { InstancesScreen } from './src/screens/InstancesScreen';
import { PluginsScreen } from './src/screens/PluginsScreen';
import { ScenariosScreen } from './src/screens/ScenariosScreen';
import { SettingsScreen } from './src/screens/SettingsScreen';
import { InstanceDetailScreen } from './src/screens/InstanceDetailScreen';
import { PluginDetailScreen } from './src/screens/PluginDetailScreen';
import { ScenarioDetailScreen } from './src/screens/ScenarioDetailScreen';
import { LoginScreen } from './src/screens/LoginScreen';

// Services
import { api } from './src/services/api';
import { socket } from './src/services/socket';
import { authStore } from './src/stores/authStore';
import { instanceStore } from './src/stores/instanceStore';
import { pluginStore } from './src/stores/pluginStore';

// Theme
import { theme } from './src/theme';

// Storage
export const storage = new MMKV();
export const zustandStorage: PersistStorage<any> = {
  getItem: (name) => {
    const value = storage.getString(name);
    return value ?? null;
  },
  setItem: (name, value) => {
    storage.set(name, value);
  },
  removeItem: (name) => {
    storage.delete(name);
  },
};

// Query Client
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

const Stack = createStackNavigator();
const Tab = createBottomTabNavigator();

function TabNavigator() {
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        tabBarIcon: ({ focused, color, size }) => {
          let iconName;
          switch (route.name) {
            case 'Dashboard':
              iconName = focused ? 'view-dashboard' : 'view-dashboard-outline';
              break;
            case 'Instances':
              iconName = focused ? 'server' : 'server-outline';
              break;
            case 'Plugins':
              iconName = focused ? 'puzzle' : 'puzzle-outline';
              break;
            case 'Scenarios':
              iconName = focused ? 'file-document' : 'file-document-outline';
              break;
            case 'Settings':
              iconName = focused ? 'cog' : 'cog-outline';
              break;
          }
          return <Icon name={iconName} size={size} color={color} />;
        },
        tabBarActiveTintColor: theme.colors.primary,
        tabBarInactiveTintColor: theme.colors.onSurfaceVariant,
        headerShown: false,
      })}
    >
      <Tab.Screen name="Dashboard" component={DashboardScreen} />
      <Tab.Screen name="Instances" component={InstancesScreen} />
      <Tab.Screen name="Plugins" component={PluginsScreen} />
      <Tab.Screen name="Scenarios" component={ScenariosScreen} />
      <Tab.Screen name="Settings" component={SettingsScreen} />
    </Tab.Navigator>
  );
}

function AuthStack() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="Login" component={LoginScreen} />
    </Stack.Navigator>
  );
}

function MainStack() {
  return (
    <Stack.Navigator screenOptions={{ headerStyle: { backgroundColor: theme.colors.surface } }}>
      <Stack.Screen name="MainTabs" component={TabNavigator} />
      <Stack.Screen name="InstanceDetail" component={InstanceDetailScreen} />
      <Stack.Screen name="PluginDetail" component={PluginDetailScreen} />
      <Stack.Screen name="ScenarioDetail" component={ScenarioDetailScreen} />
    </Stack.Navigator>
  );
}

export default function App() {
  const isAuthenticated = authStore((state) => state.isAuthenticated);

  return (
    <PaperProvider theme={theme}>
      <GestureHandlerRootView style={{ flex: 1 }}>
        <QueryClientProvider client={queryClient}>
          <NavigationContainer>
            <Stack.Navigator screenOptions={{ headerShown: false }}>
              {isAuthenticated ? (
                <Stack.Screen name="Main" component={MainStack} />
              ) : (
                <Stack.Screen name="Auth" component={AuthStack} />
              )}
            </Stack.Navigator>
          </NavigationContainer>
        </QueryClientProvider>
        <Toast
          position="top"
          textStyle={{ fontSize: 14 }}
          visibilityTime={4000}
          autoHide
        />
      </GestureHandlerRootView>
    </PaperProvider>
  );
}

// Icon component
import Icon from './src/components/Icon';

