import React from 'react';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { useNotifications } from '../context/NotificationContext';
import { IconButton, Text, Badge } from 'react-native-paper';
import { Ionicons, MaterialCommunityIcons } from '@expo/vector-icons';

// Screens
import { DashboardScreen } from '../screens/DashboardScreen';
import { InstancesScreen } from '../screens/InstancesScreen';
import { JobsScreen } from '../screens/JobsScreen';
import { PluginsScreen } from '../screens/PluginsScreen';
import { MarketplaceScreen } from '../screens/MarketplaceScreen';
import { AIModelsScreen } from '../screens/AIModelsScreen';
import { AIDatasetsScreen } from '../screens/AIDatasetsScreen';
import { AIExperimentsScreen } from '../screens/AIExperimentsScreen';
import { UsersScreen } from '../screens/UsersScreen';
import { SettingsScreen } from '../screens/SettingsScreen';
import { LoginScreen } from '../screens/LoginScreen';
import { NotificationsScreen } from '../screens/NotificationsScreen';
import { ProfileScreen } from '../screens/ProfileScreen';

const Stack = createNativeStackNavigator();
const Tab = createBottomTabNavigator();

const MainTabs = () => {
  const { isDark } = useTheme();
  const { unreadCount } = useNotifications();

  const tabBarIcon = (name: string, focused: boolean, color: string) => (
    <Ionicons name={focused ? name : name.replace('-sharp', '').replace('-outline', '')} size={26} color={color} />
  );

  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        tabBarIcon: ({ focused, color, size }) => {
          let iconName: string;
          switch (route.name) {
            case 'Dashboard':
              iconName = focused ? 'home-sharp' : 'home-outline';
              break;
            case 'Instances':
              iconName = focused ? 'phone-portrait-sharp' : 'phone-portrait-outline';
              break;
            case 'Jobs':
              iconName = focused ? 'briefcase-sharp' : 'briefcase-outline';
              break;
            case 'Plugins':
              iconName = focused ? 'puzzle-sharp' : 'puzzle-outline';
              break;
            case 'Marketplace':
              iconName = focused ? 'storefront-sharp' : 'storefront-outline';
              break;
            case 'AI':
              iconName = focused ? 'brain-sharp' : 'brain-outline';
              break;
            case 'More':
              iconName = focused ? 'ellipsis-horizontal-sharp' : 'ellipsis-horizontal-outline';
              break;
            default:
              iconName = 'help-outline';
          }
          return <Ionicons name={iconName} size={size} color={color} />;
        },
        tabBarActiveTintColor: '#1976D2',
        tabBarInactiveTintColor: '#757575',
        headerShown: false,
      })}
    >
      <Tab.Screen name="Dashboard" component={DashboardScreen} />
      <Tab.Screen name="Instances" component={InstancesScreen} />
      <Tab.Screen name="Jobs" component={JobsScreen} />
      <Tab.Screen name="Plugins" component={PluginsScreen} />
      <Tab.Screen name="Marketplace" component={MarketplaceScreen} />
      <Tab.Screen name="AI" component={AIStack} />
      <Tab.Screen name="More" component={MoreStack} />
    </Tab.Navigator>
  );
};

const AIStack = () => {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="AIModels" component={AIModelsScreen} />
      <Stack.Screen name="AIDatasets" component={AIDatasetsScreen} />
      <Stack.Screen name="AIExperiments" component={AIExperimentsScreen} />
    </Stack.Navigator>
  );
};

const MoreStack = () => {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="Users" component={UsersScreen} />
      <Stack.Screen name="Notifications" component={NotificationsScreen} />
      <Stack.Screen name="Profile" component={ProfileScreen} />
      <Stack.Screen name="Settings" component={SettingsScreen} />
    </Stack.Navigator>
  );
};

export const AppNavigator = () => {
  const { isAuthenticated, isLoading } = useAuth();
  const { isDark } = useTheme();

  if (isLoading) {
    return (
      <Stack.Navigator screenOptions={{ headerShown: false }}>
        <Stack.Screen name="Loading" component={LoadingScreen} />
      </Stack.Navigator>
    );
  }

  return (
    <Stack.Navigator
      screenOptions={{
        headerStyle: {
          backgroundColor: isDark ? '#1E1E1E' : '#FFFFFF',
        },
        headerTintColor: isDark ? '#FFFFFF' : '#212121',
      }}
    >
      {isAuthenticated ? (
        <>
          <Stack.Screen name="Main" component={MainTabs} options={{ headerShown: false }} />
          <Stack.Screen name="InstanceDetail" component={InstanceDetailScreen} />
          <Stack.Screen name="JobDetail" component={JobDetailScreen} />
          <Stack.Screen name="PluginDetail" component={PluginDetailScreen} />
          <Stack.Screen name="MarketplaceDetail" component={MarketplaceDetailScreen} />
          <Stack.Screen name="ModelDetail" component={ModelDetailScreen} />
          <Stack.Screen name="DatasetDetail" component={DatasetDetailScreen} />
          <Stack.Screen name="ExperimentDetail" component={ExperimentDetailScreen} />
          <Stack.Screen name="PipelineRunDetail" component={PipelineRunDetailScreen} />
          <Stack.Screen name="UserDetail" component={UserDetailScreen} />
        </>
      ) : (
        <>
          <Stack.Screen name="Login" component={LoginScreen} options={{ headerShown: false }} />
          <Stack.Screen name="Register" component={RegisterScreen} />
        </>
      )}
    </Stack.Navigator>
  );
};

const LoadingScreen = () => {
  const { colors } = useTheme();
  return (
    <React.Fragment>
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: colors.background }}>
        <ActivityIndicator size="large" color={colors.primary} />
        <Text style={{ marginTop: 16, color: colors.onSurface }}>Loading KKBOT...</Text>
      </View>
    </React.Fragment>
  );
};

// Placeholder screens - to be implemented
const InstanceDetailScreen = () => <ScreenPlaceholder title="Instance Detail" />;
const JobDetailScreen = () => <ScreenPlaceholder title="Job Detail" />;
const PluginDetailScreen = () => <ScreenPlaceholder title="Plugin Detail" />;
const MarketplaceDetailScreen = () => <ScreenPlaceholder title="Marketplace Detail" />;
const ModelDetailScreen = () => <ScreenPlaceholder title="Model Detail" />;
const DatasetDetailScreen = () => <ScreenPlaceholder title="Dataset Detail" />;
const ExperimentDetailScreen = () => <ScreenPlaceholder title="Experiment Detail" />;
const PipelineRunDetailScreen = () => <ScreenPlaceholder title="Pipeline Run Detail" />;
const UserDetailScreen = () => <ScreenPlaceholder title="User Detail" />;
const RegisterScreen = () => <ScreenPlaceholder title="Register" />;

const ScreenPlaceholder = ({ title }: { title: string }) => {
  const { colors } = useTheme();
  return (
    <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: colors.background, padding: 16 }}>
      <Text style={{ fontSize: 24, color: colors.onSurface }}>{title}</Text>
      <Text style={{ marginTop: 8, color: colors.onSurfaceVariant }}>Screen implementation pending</Text>
    </View>
  );
};

// Import React components
import { View, Text, ActivityIndicator } from 'react-native';
import { useTheme } from '../context/ThemeContext';
import { useAuth } from '../context/AuthContext';
import { Stack } from './AppNavigator';