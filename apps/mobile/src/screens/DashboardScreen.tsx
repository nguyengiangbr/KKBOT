import React, { useEffect, useState } from 'react';
import { 
  View, 
  Text, 
  ScrollView, 
  RefreshControl, 
  ActivityIndicator,
  StyleSheet,
  TouchableOpacity,
  Image
} from 'react-native';
import { Card, Title, Paragraph, Button, Chip } from 'react-native-paper';
import { LineChart, Grid } from 'react-native-chart-kit';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow } from 'date-fns';
import { api } from '../services/api';
import { instanceStore } from '../stores/instanceStore';
import { theme } from '../theme';

export const DashboardScreen = () => {
  const [refreshing, setRefreshing] = useState(false);
  const queryClient = useQueryClient();

  const { data: instances, isLoading: instancesLoading } = useQuery({
    queryKey: ['instances'],
    queryFn: () => api.getInstances(),
    refetchInterval: 30000,
  });

  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ['dashboardStats'],
    queryFn: () => api.getDashboardStats(),
    refetchInterval: 60000,
  });

  const { data: recentActivity } = useQuery({
    queryKey: ['recentActivity'],
    queryFn: () => api.getRecentActivity({ limit: 10 }),
  });

  const startInstanceMutation = useMutation({
    mutationFn: (id: string) => api.startInstance(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instances'] });
      queryClient.invalidateQueries({ queryKey: ['dashboardStats'] });
    },
  });

  const stopInstanceMutation = useMutation({
    mutationFn: (id: string) => api.stopInstance(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['instances'] });
      queryClient.invalidateQueries({ queryKey: ['dashboardStats'] });
    },
  });

  const onRefresh = async () => {
    setRefreshing(true);
    await queryClient.invalidateQueries({ queryKey: ['instances', 'dashboardStats'] });
    setRefreshing(false);
  };

  if (instancesLoading || statsLoading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={theme.colors.primary} />
        <Text style={styles.loadingText}>Loading dashboard...</Text>
      </View>
    );
  }

  const activeInstances = instances?.filter(i => i.state === 'Running').length || 0;
  const totalInstances = instances?.length || 0;

  return (
    <ScrollView
      refreshControl={
        <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
      }
      style={styles.container}
      contentContainerStyle={styles.contentContainer}
    >
      {/* Header */}
      <View style={styles.header}>
        <View style={styles.headerContent}>
          <Text style={styles.greeting}>Welcome back</Text>
          <Title style={styles.title}>KKBOT Dashboard</Title>
        </View>
        <View style={styles.headerActions}>
          <Chip
            icon="server"
            style={styles.statusChip}
            color={activeInstances > 0 ? theme.colors.success : theme.colors.outline}
          >
            {activeInstances}/{totalInstances} Active
          </Chip>
        </View>
      </View>

      {/* Stats Grid */}
      <View style={styles.statsGrid}>
        <StatCard
          title="Total Instances"
          value={totalInstances}
          icon="server"
          color={theme.colors.primary}
          trend="+2 this week"
        />
        <StatCard
          title="Active"
          value={activeInstances}
          icon="play-circle"
          color={theme.colors.success}
          trend={activeInstances > 0 ? "Running" : "Idle"}
        />
        <StatCard
          title="CPU Usage"
          value={`${stats?.avgCpuUsage?.toFixed(1) || 0}%`}
          icon="cpu-64-bit"
          color={theme.colors.warning}
          trend="Normal"
        />
        <StatCard
          title="Memory"
          value={`${stats?.avgMemoryUsage?.toFixed(1) || 0}%`}
          icon="memory"
          color={theme.colors.info}
          trend="Stable"
        />
      </View>

      {/* Performance Chart */}
      <Card style={styles.chartCard}>
        <Card.Content>
          <Title style={styles.chartTitle}>Performance Overview (24h)</Title>
          {stats?.cpuHistory && (
            <LineChart
              data={{
                labels: stats.cpuHistory.map((_, i) => `${i}h`),
                datasets: [
                  {
                    label: 'CPU %',
                    data: stats.cpuHistory,
                    color: theme.colors.primary,
                    strokeWidth: 2,
                  },
                  {
                    label: 'Memory %',
                    data: stats.memoryHistory || [],
                    color: theme.colors.info,
                    strokeWidth: 2,
                  },
                ],
                legend: ['CPU %', 'Memory %'],
              }}
              width={350}
              height={200}
              chartConfig={{
                backgroundColor: theme.colors.surface,
                backgroundGradientFrom: theme.colors.surface,
                backgroundGradientTo: theme.colors.surface,
                decimalPlaces: 1,
                color: (opacity = 1) => `rgba(255, 255, 255, ${opacity})`,
                style: {
                  borderRadius: 16,
                },
                propsForDots: {
                  r: 4,
                  strokeWidth: 2,
                  stroke: theme.colors.background,
                },
              }}
              bezier
              style={styles.chart}
            />
          )}
        </Card.Content>
      </Card>

      {/* Recent Instances */}
      <View style={styles.section}>
        <View style={styles.sectionHeader}>
          <Title style={styles.sectionTitle}>Recent Instances</Title>
          <Button mode="text" onPress={() => navigation.navigate('Instances')}>
            View All
          </Button>
        </View>
        {instances?.slice(0, 5).map((instance) => (
          <InstanceCard
            key={instance.id}
            instance={instance}
            onStart={() => startInstanceMutation.mutate(instance.id)}
            onStop={() => stopInstanceMutation.mutate(instance.id)}
            starting={startInstanceMutation.variables === instance.id}
            stopping={stopInstanceMutation.variables === instance.id}
          />
        ))}
      </View>

      {/* Recent Activity */}
      {recentActivity?.length > 0 && (
        <View style={styles.section}>
          <View style={styles.sectionHeader}>
            <Title style={styles.sectionTitle}>Recent Activity</Title>
          </View>
          <Card style={styles.activityCard}>
            <Card.Content>
              {recentActivity.slice(0, 5).map((activity, index) => (
                <ActivityItem key={activity.id} activity={activity} />
              ))}
            </Card.Content>
          </Card>
        </View>
      )}
    </ScrollView>
  );
};

const StatCard = ({ title, value, icon, color, trend }) => (
  <Card style={styles.statCard} elevation={2}>
    <Card.Content>
      <View style={styles.statHeader}>
        <View style={[styles.iconContainer, { backgroundColor: color + '20' }]}>
          <Icon name={icon} size={24} color={color} />
        </View>
        <Text style={[styles.trend, { color: color }]}>{trend}</Text>
      </View>
      <Text style={styles.statValue}>{value}</Text>
      <Text style={styles.statTitle}>{title}</Text>
    </Card.Content>
  </Card>
);

const InstanceCard = ({ instance, onStart, onStop, starting, stopping }) => (
  <Card style={styles.instanceCard} key={instance.id}>
    <Card.Content>
      <View style={styles.instanceHeader}>
        <View style={styles.instanceInfo}>
          <Text style={styles.instanceName}>{instance.displayName}</Text>
          <Text style={styles.instanceMeta}>
            {instance.deviceId} • {instance.module?.name || 'No Module'}
          </Text>
        </View>
        <InstanceStatusBadge state={instance.state} />
      </View>
      <View style={styles.instanceActions}>
        {instance.state === 'Running' ? (
          <Button
            mode="outlined"
            onPress={onStop}
            disabled={stopping}
            style={styles.actionButton}
            icon="stop"
          >
            {stopping ? 'Stopping...' : 'Stop'}
          </Button>
        ) : (
          <Button
            mode="contained"
            onPress={onStart}
            disabled={starting}
            style={styles.actionButton}
            icon="play"
          >
            {starting ? 'Starting...' : 'Start'}
          </Button>
        )}
        <Button
          mode="text"
          onPress={() => navigation.navigate('InstanceDetail', { id: instance.id })}
          style={styles.actionButton}
        >
          Details
        </Button>
      </View>
    </Card.Content>
  </Card>
);

const InstanceStatusBadge = ({ state }) => {
  const colors = {
    Running: theme.colors.success,
    Starting: theme.colors.warning,
    Stopping: theme.colors.warning,
    Stopped: theme.colors.outline,
    Error: theme.colors.error,
    Idle: theme.colors.info,
  };

  return (
    <View style={[styles.statusBadge, { backgroundColor: colors[state] + '20' }]}>
      <Text style={[styles.statusText, { color: colors[state] }]}>
        {state}
      </Text>
    </View>
  );
};

const ActivityItem = ({ activity }) => (
  <View style={styles.activityItem}>
    <View style={styles.activityIcon}>
      <Icon 
        name={getActivityIcon(activity.type)} 
        size={20} 
        color={theme.colors.primary} 
      />
    </View>
    <View style={styles.activityContent}>
      <Text style={styles.activityTitle}>{activity.title}</Text>
      <Text style={styles.activityDescription}>{activity.description}</Text>
    </View>
    <Text style={styles.activityTime}>
      {formatDistanceToNow(new Date(activity.timestamp), { addSuffix: true })}
    </Text>
  </View>
);

const getActivityIcon = (type: string) => {
  switch (type) {
    case 'instance_started': return 'play-circle';
    case 'instance_stopped': return 'stop-circle';
    case 'plugin_loaded': return 'puzzle';
    case 'scenario_started': return 'play';
    case 'error': return 'alert-circle';
    default: return 'information';
  }
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: theme.colors.background,
  },
  contentContainer: {
    padding: 16,
    paddingBottom: 100,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 12,
    color: theme.colors.onSurfaceVariant,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 24,
  },
  headerContent: {
    flex: 1,
  },
  greeting: {
    fontSize: 14,
    color: theme.colors.onSurfaceVariant,
    marginBottom: 4,
  },
  title: {
    fontSize: 28,
    fontWeight: '700',
    color: theme.colors.onSurface,
  },
  headerActions: {
    marginTop: 8,
  },
  statusChip: {
    marginTop: 4,
  },
  statsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    marginBottom: 24,
  },
  statCard: {
    width: '48%',
    marginBottom: 12,
  },
  statHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  iconContainer: {
    width: 40,
    height: 40,
    borderRadius: 20,
    justifyContent: 'center',
    alignItems: 'center',
  },
  statValue: {
    fontSize: 28,
    fontWeight: '700',
    color: theme.colors.onSurface,
    marginBottom: 4,
  },
  statTitle: {
    fontSize: 12,
    color: theme.colors.onSurfaceVariant,
  },
  trend: {
    fontSize: 11,
    fontWeight: '500',
  },
  chartCard: {
    marginBottom: 24,
  },
  chartTitle: {
    marginBottom: 16,
  },
  chart: {
    marginVertical: 8,
    borderRadius: 16,
  },
  section: {
    marginBottom: 24,
  },
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
  },
  instanceCard: {
    marginBottom: 12,
  },
  instanceHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  instanceInfo: {
    flex: 1,
  },
  instanceName: {
    fontSize: 16,
    fontWeight: '600',
    color: theme.colors.onSurface,
    marginBottom: 2,
  },
  instanceMeta: {
    fontSize: 12,
    color: theme.colors.onSurfaceVariant,
  },
  instanceActions: {
    flexDirection: 'row',
    gap: 8,
    flexWrap: 'wrap',
  },
  actionButton: {
    flex: 1,
    minWidth: 100,
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 4,
    borderRadius: 12,
  },
  statusText: {
    fontSize: 11,
    fontWeight: '600',
    textTransform: 'uppercase',
  },
  activityCard: {
    padding: 8,
  },
  activityItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: theme.colors.outlineVariant,
  },
  activityIcon: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: theme.colors.primary + '15',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  activityContent: {
    flex: 1,
  },
  activityTitle: {
    fontSize: 14,
    fontWeight: '500',
    color: theme.colors.onSurface,
    marginBottom: 2,
  },
  activityDescription: {
    fontSize: 12,
    color: theme.colors.onSurfaceVariant,
  },
  activityTime: {
    fontSize: 11,
    color: theme.colors.onSurfaceVariant,
    marginLeft: 12,
  },
});

import { navigation } from '../navigation/rootNavigation';
