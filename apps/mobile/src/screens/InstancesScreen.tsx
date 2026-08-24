import React from 'react';
import { FlatList, RefreshControl, ActivityIndicator, View, Text } from 'react-native';
import { Card, Title, Button, Chip, Searchbar } from 'react-native-paper';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import { theme } from '../theme';
import { InstanceStatusBadge } from '../components/InstanceStatusBadge';
import { Icon } from '../components/Icon';

export const InstancesScreen = ({ navigation }) => {
  const [refreshing, setRefreshing] = React.useState(false);
  const [searchQuery, setSearchQuery] = React.useState('');
  const queryClient = useQueryClient();

  const { data: instances, isLoading } = useQuery({
    queryKey: ['instances', searchQuery],
    queryFn: () => api.getInstances({ search: searchQuery }),
    refetchInterval: 30000,
    enabled: !isLoading,
  });

  const startMutation = useMutation({
    mutationFn: (id: string) => api.startInstance(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['instances'] }),
  });

  const stopMutation = useMutation({
    mutationFn: (id: string) => api.stopInstance(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['instances'] }),
  });

  const restartMutation = useMutation({
    mutationFn: (id: string) => api.restartInstance(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['instances'] }),
  });

  const onRefresh = async () => {
    setRefreshing(true);
    await queryClient.invalidateQueries({ queryKey: ['instances'] });
    setRefreshing(false);
  };

  const filteredInstances = instances?.filter(instance =>
    instance.displayName.toLowerCase().includes(searchQuery.toLowerCase()) ||
    instance.deviceId.toLowerCase().includes(searchQuery.toLowerCase()) ||
    instance.module?.name?.toLowerCase().includes(searchQuery.toLowerCase())
  ) || [];

  if (isLoading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={theme.colors.primary} />
        <Text style={styles.loadingText}>Loading instances...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Searchbar
        placeholder="Search instances..."
        onChangeText={setSearchQuery}
        value={searchQuery}
        style={styles.searchbar}
      />

      <FlatList
        data={filteredInstances}
        keyExtractor={item => item.id}
        renderItem={({ item }) => (
          <InstanceListItem
            instance={item}
            onPress={() => navigation.navigate('InstanceDetail', { id: item.id })}
            onStart={startMutation.mutate}
            onStop={stopMutation.mutate}
            onRestart={restartMutation.mutate}
            starting={startMutation.variables === item.id}
            stopping={stopMutation.variables === item.id}
            restarting={restartMutation.variables === item.id}
          />
        )}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
        ListEmptyComponent={
          <View style={styles.emptyContainer}>
            <Icon name="server-off" size={64} color={theme.colors.outline} />
            <Text style={styles.emptyText}>
              {searchQuery ? 'No instances found' : 'No instances yet'}
            </Text>
            {!searchQuery && (
              <Button mode="contained" onPress={() => navigation.navigate('CreateInstance')}>
                Create Instance
              </Button>
            )}
          </View>
        }
        contentContainerStyle={styles.listContent}
      />
    </View>
  );
};

const InstanceListItem = ({ instance, onPress, onStart, onStop, onRestart, starting, stopping, restarting }) => (
  <Card style={styles.card} onPress={onPress}>
    <Card.Content>
      <View style={styles.itemHeader}>
        <View style={styles.itemInfo}>
          <Text style={styles.instanceName}>{instance.displayName}</Text>
          <Text style={styles.instanceMeta}>
            {instance.deviceId}
          </Text>
        </View>
        <InstanceStatusBadge state={instance.state} />
      </View>

      <View style={styles.itemDetails}>
        <View style={styles.detailItem}>
          <Icon name="puzzle" size={16} color={theme.colors.onSurfaceVariant} />
          <Text style={styles.detailText}>
            {instance.module?.name || 'No Module'}
          </Text>
        </View>
        <View style={styles.detailItem}>
          <Icon name="cog" size={16} color={theme.colors.onSurfaceVariant} />
          <Text style={styles.detailText}>
            v{instance.version || '1.0.0'}
          </Text>
        </View>
        <View style={styles.detailItem}>
          <Icon name="calendar" size={16} color={theme.colors.onSurfaceVariant} />
          <Text style={styles.detailText}>
            Updated {formatDistance(instance.updatedAt)}
          </Text>
        </View>
      </View>

      <View style={styles.itemActions}>
        {instance.state === 'Running' ? (
          <>
            <Button
              mode="outlined"
              onPress={() => onStop(instance.id)}
              disabled={stopping}
              style={styles.actionButton}
              icon="stop"
            >
              {stopping ? 'Stopping...' : 'Stop'}
            </Button>
            <Button
              mode="outlined"
              onPress={() => onRestart(instance.id)}
              disabled={restarting}
              style={styles.actionButton}
              icon="restart"
            >
              {restarting ? 'Restarting...' : 'Restart'}
            </Button>
          </>
        ) : instance.state === 'Error' ? (
          <>
            <Button
              mode="outlined"
              onPress={() => onRestart(instance.id)}
              disabled={restarting}
              style={styles.actionButton}
              icon="restart"
            >
              {restarting ? 'Restarting...' : 'Restart'}
            </Button>
            <Button
              mode="contained"
              onPress={() => onStart(instance.id)}
              disabled={starting}
              style={styles.actionButton}
              icon="play"
            >
              {starting ? 'Starting...' : 'Start'}
            </Button>
          </>
        ) : (
          <Button
            mode="contained"
            onPress={() => onStart(instance.id)}
            disabled={starting}
            style={styles.actionButton}
            icon="play"
          >
            {starting ? 'Starting...' : 'Start'}
          </Button>
        )}
      </View>
    </Card.Content>
  </Card>
);

const styles = {
  container: {
    flex: 1,
    backgroundColor: theme.colors.background,
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
  searchbar: {
    margin: 16,
  },
  listContent: {
    padding: 16,
    paddingBottom: 100,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  emptyText: {
    marginTop: 16,
    fontSize: 16,
    color: theme.colors.onSurfaceVariant,
    textAlign: 'center',
  },
  card: {
    marginBottom: 12,
    elevation: 1,
  },
  itemHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  itemInfo: {
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
  itemDetails: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 16,
    marginBottom: 12,
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: theme.colors.outlineVariant,
  },
  detailItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  detailText: {
    fontSize: 12,
    color: theme.colors.onSurfaceVariant,
  },
  itemActions: {
    flexDirection: 'row',
    gap: 8,
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: theme.colors.outlineVariant,
  },
};

import { formatDistance } from 'date-fns';
