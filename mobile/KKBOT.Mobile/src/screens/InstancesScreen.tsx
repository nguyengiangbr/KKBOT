import React, { useEffect, useState } from 'react';
import { View, Text, FlatList, ActivityIndicator, RefreshControl, Alert } from 'react-native';
import { Card, Title, Paragraph, Chip, Button, IconButton, FAB, TextInput, Snackbar } from 'react-native-paper';
import { Ionicons, MaterialCommunityIcons } from '@expo/vector-icons';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { api } from '../services/api';
import { Instance, InstanceStatus } from '../types/auth';
import { useNavigation } from '@react-navigation/native';
import { styles, listStyles } from './styles';

export const InstancesScreen = () => {
  const { colors } = useTheme();
  const { isAuthenticated } = useAuth();
  const navigation = useNavigation();
  const [isLoading, setIsLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [instances, setInstances] = useState<Instance[]>([]);
  const [filteredInstances, setFilteredInstances] = useState<Instance[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<InstanceStatus | 'All'>('All');
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [newInstanceName, setNewInstanceName] = useState('');
  const [newInstanceModuleId, setNewInstanceModuleId] = useState('');
  const [newInstanceDeviceId, setNewInstanceDeviceId] = useState('');
  const [snackbarVisible, setSnackbarVisible] = useState(false);
  const [snackbarMessage, setSnackbarMessage] = useState('');

  const loadInstances = async () => {
    if (!isAuthenticated) return;
    
    try {
      const response = await api.getInstances({ pageSize: 100 });
      const data = response.data.items || response.data;
      setInstances(data);
      setFilteredInstances(data);
    } catch (error) {
      console.error('Failed to load instances:', error);
      showSnackbar('Failed to load instances');
    } finally {
      setIsLoading(false);
      setRefreshing(false);
    }
  };

  const onRefresh = () => {
    setRefreshing(true);
    loadInstances();
  };

  const filterInstances = () => {
    let result = instances;
    
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      result = result.filter(i => 
        i.name.toLowerCase().includes(query) || 
        i.id.toLowerCase().includes(query)
      );
    }
    
    if (statusFilter !== 'All') {
      result = result.filter(i => i.status === statusFilter);
    }
    
    setFilteredInstances(result);
  };

  const handleCreateInstance = async () => {
    if (!newInstanceName.trim()) {
      showSnackbar('Instance name is required');
      return;
    }

    try {
      await api.createInstance({
        name: newInstanceName.trim(),
        moduleId: newInstanceModuleId || undefined,
        deviceId: newInstanceDeviceId || undefined,
      });
      
      showSnackbar('Instance created successfully');
      setShowCreateDialog(false);
      setNewInstanceName('');
      setNewInstanceModuleId('');
      setNewInstanceDeviceId('');
      loadInstances();
    } catch (error) {
      showSnackbar('Failed to create instance');
    }
  };

  const handleStartInstance = async (id: string) => {
    try {
      await api.startInstance(id);
      showSnackbar('Instance started');
      loadInstances();
    } catch (error) {
      showSnackbar('Failed to start instance');
    }
  };

  const handleStopInstance = async (id: string) => {
    try {
      await api.stopInstance(id);
      showSnackbar('Instance stopped');
      loadInstances();
    } catch (error) {
      showSnackbar('Failed to stop instance');
    }
  };

  const handleDeleteInstance = async (id: string) => {
    Alert.alert(
      'Delete Instance',
      'Are you sure you want to delete this instance? This action cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        { 
          text: 'Delete', 
          style: 'destructive',
          onPress: async () => {
            try {
              await api.deleteInstance(id);
              showSnackbar('Instance deleted');
              loadInstances();
            } catch (error) {
              showSnackbar('Failed to delete instance');
            }
          }
        },
      ]
    );
  };

  const showSnackbar = (message: string) => {
    setSnackbarMessage(message);
    setSnackbarVisible(true);
  };

  useEffect(() => {
    if (isAuthenticated) {
      loadInstances();
    }
  }, [isAuthenticated]);

  useEffect(() => {
    filterInstances();
  }, [instances, searchQuery, statusFilter]);

  if (isLoading) {
    return (
      <View style={[styles.container, { backgroundColor: colors.background }]}>
        <ActivityIndicator size="large" color={colors.primary} style={styles.loadingContainer} />
      </View>
    );
  }

  const statusColors: Record<InstanceStatus, string> = {
    Running: '#4CAF50',
    Stopped: '#F44336',
    Created: '#1976D2',
    Error: '#F44336',
    Paused: '#FF9800',
  };

  return (
    <View style={[styles.container, { backgroundColor: colors.background }]}>
      <ScrollView
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[colors.primary]} />
        }
        contentContainerStyle={styles.scrollContent}
      >
        {/* Search & Filter */}
        <Card style={styles.card} elevation={1}>
          <Card.Content style={{ padding: 16 }}>
            <TextInput
              label="Search instances"
              value={searchQuery}
              onChangeText={setSearchQuery}
              mode="outlined"
              style={styles.searchInput}
              placeholder="Search by name..."
              left={<Ionicons name="search" size={24} color={colors.onSurfaceVariant} />}
            />
            <TextInput
              label="Status Filter"
              value={statusFilter}
              mode="outlined"
              style={[styles.filterInput, { marginTop: 12 }]}
              right={<Ionicons name="chevron-down" size={24} color={colors.onSurfaceVariant} />}
              editable={false}
              onFocus={() => {
                // Would show a picker modal in real implementation
              }}
            />
          </Card.Content>
        </Card>

        {/* Stats Summary */}
        <View style={styles.statsRow}>
          <StatItem
            label="Total"
            value={instances.length}
            color={colors.primary}
            icon="server"
          />
          <StatItem
            label="Running"
            value={instances.filter(i => i.status === 'Running').length}
            color="#4CAF50"
            icon="play-circle"
          />
          <StatItem
            label="Stopped"
            value={instances.filter(i => i.status === 'Stopped').length}
            color="#F44336"
            icon="stop-circle"
          />
          <StatItem
            label="Errors"
            value={instances.filter(i => i.status === 'Error').length}
            color="#F44336"
            icon="alert-circle"
          />
        </View>

        {/* Instances List */}
        {filteredInstances.length === 0 ? (
          <Card style={styles.emptyCard} elevation={1}>
            <Card.Content style={{ padding: 48, alignItems: 'center' }}>
              <MaterialCommunityIcons name="server-off" size={64} color={colors.onSurfaceVariant} />
              <Title style={{ marginTop: 16, color: colors.onSurface }}>No Instances</Title>
              <Paragraph style={{ marginTop: 8, color: colors.onSurfaceVariant, textAlign: 'center' }}>
                {searchQuery || statusFilter !== 'All' 
                  ? 'No instances match your filters'
                  : 'Create your first instance to get started'}
              </Paragraph>
              <Button
                mode="contained"
                onPress={() => setShowCreateDialog(true)}
                style={{ marginTop: 16 }}
              >
                Create Instance
              </Button>
            </Card.Content>
          </Card>
        ) : (
          <FlatList
            data={filteredInstances}
            keyExtractor={item => item.id}
            renderItem={({ item }) => (
              <InstanceListItem
                instance={item}
                onPress={() => navigation.navigate('InstanceDetail', { instanceId: item.id })}
                onStart={handleStartInstance}
                onStop={handleStopInstance}
                onDelete={handleDeleteInstance}
              />
            )}
            keyExtractor={item => item.id}
            ItemSeparatorComponent={() => <View style={styles.separator} />}
            ListEmptyComponent={
              <Card style={styles.emptyCard} elevation={1}>
                <Card.Content style={{ padding: 48, alignItems: 'center' }}>
                  <MaterialCommunityIcons name="server-off" size={64} color={colors.onSurfaceVariant} />
                  <Title style={{ marginTop: 16, color: colors.onSurface }}>No Instances Found</Title>
                  <Paragraph style={{ marginTop: 8, color: colors.onSurfaceVariant }}>Try adjusting your search or filter</Paragraph>
                </Card.Content>
              </Card>
            }
          />
        )}
      </ScrollView>

      <FAB
        style={styles.fab}
        icon="plus"
        onPress={() => setShowCreateDialog(true)}
        label="New Instance"
      />

      {/* Create Instance Dialog */}
      <InstanceCreateDialog
        visible={showCreateDialog}
        onClose={() => setShowCreateDialog(false)}
        onCreate={handleCreateInstance}
      />

      <Snackbar
        visible={snackbarVisible}
        onDismiss={() => setSnackbarVisible(false)}
        action={{ label: 'OK', onPress: () => setSnackbarVisible(false) }}
      >
        {snackbarMessage}
      </Snackbar>
    </View>
  );
};

const StatItem = ({ label, value, color, icon }: { label: string; value: number; color: string; icon: string }) => {
  const { colors } = useTheme();
  return (
    <View style={styles.statItem}>
      <View style={[styles.statIcon, { backgroundColor: color }]}>
        <MaterialCommunityIcons name={icon} size={20} color="white" />
      </View>
      <Text style={[styles.statValue, { color }]}>{value}</Text>
      <Text style={[styles.statLabel, { color: colors.onSurfaceVariant }]}>{label}</Text>
    </View>
  );
};

const InstanceListItem = ({ instance, onPress, onStart, onStop, onDelete }: { 
  instance: any; 
  onPress: () => void;
  onStart: (id: string) => void;
  onStop: (id: string) => void;
  onDelete: (id: string) => void;
}) => {
  const { colors } = useTheme();
  const statusColors: Record<string, string> = {
    Running: '#4CAF50',
    Stopped: '#F44336',
    Created: '#1976D2',
    Error: '#F44336',
    Paused: '#FF9800',
  };

  return (
    <Card style={[styles.listCard, listStyles.item]} elevation={1} onPress={onPress}>
      <Card.Content style={{ padding: 16 }}>
        <View style={styles.instanceListRow}>
          <View style={styles.instanceIcon}>
            <MaterialCommunityIcons name="server" size={32} color={colors.primary} />
          </View>
          <View style={styles.instanceListInfo}>
            <Title style={{ color: colors.onSurface }}>{instance.name}</Title>
            <Paragraph style={{ color: colors.onSurfaceVariant }}>ID: {instance.id.slice(0, 8)}...</Paragraph>
          </View>
          <View style={styles.instanceActions}>
            <Chip
              style={{ backgroundColor: statusColors[instance.status] || colors.primary }}
              mode="outlined"
            >
              {instance.status}
            </Chip>
            <View style={{ flexDirection: 'row', marginTop: 8 }}>
              {instance.status !== 'Running' && (
                <Button
                  mode="text"
                  icon="play"
                  onPress={() => handleStart(instance.id)}
                  style={{ marginRight: 8 }}
                >
                  Start
                </Button>
              )}
              {instance.status === 'Running' && (
                <Button
                  mode="text"
                  icon="stop"
                  color="#F44336"
                  onPress={() => handleStop(instance.id)}
                  style={{ marginRight: 8 }}
                >
                  Stop
                </Button>
              )}
              <IconButton
                icon="delete"
                color="#F44336"
                onPress={() => handleDelete(instance.id)}
              />
            </View>
          </View>
        </View>
      </Card.Content>
    </Card>
  );
};

const CreateInstanceDialog = ({ visible, onClose, onCreate }: { visible: boolean; onClose: () => void; onCreate: () => void }) => {
  // This would be a proper modal dialog component
  // For brevity, using a simple alert-based approach
  return null;
};

const handleStart = (id: string) => {
  // Would call onStart
};

const handleStop = (id: string) => {
  // Would call onStop
};

const handleDelete = (id: string) => {
  // Would call onDelete
};

import { useNavigation } from '@react-navigation/native';
import { View, Text, FlatList } from 'react-native';
import { useTheme } from '../context/ThemeContext';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { Alert } from 'react-native';