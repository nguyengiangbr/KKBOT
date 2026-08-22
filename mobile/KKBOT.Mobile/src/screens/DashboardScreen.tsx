import React, { useEffect, useState } from 'react';
import { View, ScrollView, RefreshControl, ActivityIndicator } from 'react-native';
import { Card, Title, Paragraph, Chip, Button, IconButton, FAB, Snackbar } from 'react-native-paper';
import { Ionicons, MaterialCommunityIcons } from '@expo/vector-icons';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { useNotifications } from '../context/NotificationContext';
import { api } from '../services/api';
import { Instance, Job, HealthCheckResponse } from '../types/auth';
import { useNavigation } from '@react-navigation/native';
import { styles } from './styles';

export const DashboardScreen = () => {
  const { isDark, colors } = useTheme();
  const { isAuthenticated } = useAuth();
  const { unreadCount } = useNotifications();
  const navigation = useNavigation();
  const [isLoading, setIsLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [instances, setInstances] = useState<Instance[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [health, setHealth] = useState<HealthCheckResponse | null>(null);
  const [stats, setStats] = useState({
    totalInstances: 0,
    runningInstances: 0,
    pendingJobs: 0,
    runningJobs: 0,
  });
  const [snackbarVisible, setSnackbarVisible] = useState(false);
  const [snackbarMessage, setSnackbarMessage] = useState('');

  const loadData = async () => {
    if (!isAuthenticated) return;
    
    try {
      const [instancesRes, jobsRes, healthRes] = await Promise.all([
        api.getInstances({ pageSize: 10 }),
        api.getJobs({ pageSize: 10 }),
        api.healthCheck(),
      ]);

      const instancesData = instancesRes.data.items || instancesRes.data;
      const jobsData = jobsRes.data.items || jobsRes.data;

      setInstances(instancesData);
      setJobs(jobsData);
      setHealth(healthRes.data);

      setStats({
        totalInstances: instancesData.length,
        runningInstances: instancesData.filter(i => i.status === 'Running').length,
        pendingJobs: jobsData.filter(j => j.status === 'Pending').length,
        runningJobs: jobsData.filter(j => j.status === 'Running').length,
      });
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
      showSnackbar('Failed to load dashboard data');
    } finally {
      setIsLoading(false);
      setRefreshing(false);
    }
  };

  const onRefresh = () => {
    setRefreshing(true);
    loadData();
  };

  const showSnackbar = (message: string) => {
    setSnackbarMessage(message);
    setSnackbarVisible(true);
  };

  useEffect(() => {
    if (isAuthenticated) {
      loadData();
    }
  }, [isAuthenticated]);

  if (isLoading) {
    return (
      <View style={[styles.container, { backgroundColor: colors.background }]}>
        <ActivityIndicator size="large" color={colors.primary} style={styles.loadingContainer} />
      </View>
    );
  }

  return (
    <View style={[styles.container, { backgroundColor: colors.background }]}>
      <ScrollView
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[colors.primary]} />
        }
        contentContainerStyle={styles.scrollContent}
      >
        {/* Health Status */}
        <Card style={styles.card} elevation={2}>
          <Card.Content>
            <View style={styles.healthRow}>
              <View style={[styles.healthIndicator, { backgroundColor: health?.status === 'Healthy' ? '#4CAF50' : health?.status === 'Degraded' ? '#FF9800' : '#F44336' }]} />
              <View style={styles.flex1} />
              <Text style={[styles.healthText, { color: colors.onSurface }]}>System: {health?.status || 'Unknown'}</Text>
            </View>
          </Card.Content>
        </Card>

        {/* Stats Grid */}
        <View style={styles.statsGrid}>
          <StatCard
            title="Total Instances"
            value={stats.totalInstances}
            icon="server"
            color="#1976D2"
            onPress={() => navigation.navigate('Instances')}
          />
          <StatCard
            title="Running"
            value={stats.runningInstances}
            icon="play-circle"
            color="#4CAF50"
            onPress={() => navigation.navigate('Instances')}
          />
          <StatCard
            title="Pending Jobs"
            value={stats.pendingJobs}
            icon="clock-outline"
            color="#FF9800"
            onPress={() => navigation.navigate('Jobs')}
          />
          <StatCard
            title="Running Jobs"
            value={stats.runningJobs}
            icon="play-circle"
            color="#4CAF50"
            onPress={() => navigation.navigate('Jobs')}
          />
        </View>

        {/* Recent Instances */}
        <View style={styles.sectionHeader}>
          <Title style={{ color: colors.onSurface }}>Recent Instances</Title>
          <Button mode="text" onPress={() => navigation.navigate('Instances')}>
            View All
          </Button>
        </View>

        {instances.length === 0 ? (
          <Card style={styles.card} elevation={1}>
            <Card.Content style={{ padding: 24, alignItems: 'center' }}>
              <MaterialCommunityIcons name="server-off" size={48} color={colors.onSurfaceVariant} />
              <Paragraph style={{ marginTop: 8, color: colors.onSurfaceVariant }}>No instances found</Paragraph>
              <Button mode="contained" onPress={() => navigation.navigate('Instances')} style={{ marginTop: 8 }}>
                Create Instance
              </Button>
            </Card.Content>
          </Card>
        ) : (
          <View style={styles.list}>
            {instances.slice(0, 5).map(instance => (
              <InstanceCard key={instance.id} instance={instance} onPress={() => navigation.navigate('InstanceDetail', { instanceId: instance.id })} />
            ))}
          </View>
        )}

        {/* Recent Jobs */}
        <View style={styles.sectionHeader}>
          <Title style={{ color: colors.onSurface }}>Recent Jobs</Title>
          <Button mode="text" onPress={() => navigation.navigate('Jobs')}>
            View All
          </Button>
        </View>

        {jobs.length === 0 ? (
          <Card style={styles.card} elevation={1}>
            <Card.Content style={{ padding: 24, alignItems: 'center' }}>
              <MaterialCommunityIcons name="briefcase-outline" size={48} color={colors.onSurfaceVariant} />
              <Paragraph style={{ marginTop: 8, color: colors.onSurfaceVariant }}>No jobs found</Paragraph>
              <Button mode="contained" onPress={() => navigation.navigate('Jobs')} style={{ marginTop: 8 }}>
                Create Job
              </Button>
            </Card.Content>
          </Card>
        ) : (
          <View style={styles.list}>
            {jobs.slice(0, 5).map(job => (
              <JobCard key={job.id} job={job} onPress={() => navigation.navigate('JobDetail', { jobId: job.id })} />
            ))}
          </View>
        )}

        <View style={{ height: 100 }} />
      </ScrollView>

      <FAB
        style={styles.fab}
        icon="plus"
        onPress={() => navigation.navigate('Instances')}
        label="New Instance"
      />

      <Snackbar
        visible={snackbarVisible}
        onDismiss={() => setSnackbarVisible(false)}
        action={{ label: 'Dismiss', onPress: () => setSnackbarVisible(false) }}
      >
        {snackbarMessage}
      </Snackbar>
    </View>
  );
};

const StatCard = ({ title, value, icon, color, onPress }: { title: string; value: number; icon: string; color: string; onPress: () => void }) => {
  const { colors } = useTheme();
  return (
    <Card style={[styles.statCard, { backgroundColor: color }]} onPress={onPress}>
      <Card.Content>
        <View style={styles.statCardContent}>
          <View style={[styles.statIcon, { backgroundColor: 'rgba(255,255,255,0.2)' }]}>
            <MaterialCommunityIcons name={icon} size={24} color="white" />
          </View>
          <View>
            <Text style={[styles.statValue, { color: 'white' }]}>{value}</Text>
            <Text style={[styles.statTitle, { color: 'rgba(255,255,255,0.9)' }]}>{title}</Text>
          </View>
        </View>
      </Card.Content>
    </Card>
  );
};

const InstanceCard = ({ instance, onPress }: { instance: any; onPress: () => void }) => {
  const { colors } = useTheme();
  const statusColor = instance.status === 'Running' ? '#4CAF50' : instance.status === 'Error' ? '#F44336' : '#FF9800';
  return (
    <Card style={[styles.card, styles.listItem]} elevation={1} onPress={onPress}>
      <Card.Content>
        <View style={styles.instanceRow}>
          <View style={styles.instanceInfo}>
            <Title style={{ color: colors.onSurface }}>{instance.name}</Title>
            <Paragraph style={{ color: colors.onSurfaceVariant }}>ID: {instance.id.slice(0, 8)}...</Paragraph>
          </View>
          <View style={styles.instanceStatus}>
            <Chip style={{ backgroundColor: statusColor }} mode="outlined">
              {instance.status}
            </Chip>
          </View>
        </View>
      </Card.Content>
    </Card>
  );
};

const JobCard = ({ job, onPress }: { job: any; onPress: () => void }) => {
  const { colors } = useTheme();
  const statusColor = job.status === 'Running' ? '#4CAF50' : job.status === 'Completed' ? '#4CAF50' : job.status === 'Failed' ? '#F44336' : '#FF9800';
  return (
    <Card style={[styles.card, styles.listItem]} elevation={1} onPress={onPress}>
      <Card.Content>
        <View style={styles.jobRow}>
          <View style={styles.jobInfo}>
            <Title style={{ color: colors.onSurface }}>{job.type}</Title>
            <Paragraph style={{ color: colors.onSurfaceVariant }}>Instance: {job.instanceId.slice(0, 8)}...</Paragraph>
          </View>
          <View style={styles.jobStatus}>
            <Chip style={{ backgroundColor: statusColor }} mode="outlined">
              {job.status}
            </Chip>
          </View>
        </View>
      </Card.Content>
    </Card>
  );
};

import { Text, View, Title, Paragraph } from 'react-native';
import { useTheme } from '../context/ThemeContext';
import { MaterialCommunityIcons } from '@expo/vector-icons';