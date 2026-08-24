import React from 'react';
import { 
  Grid, 
  Card, 
  CardContent, 
  CardHeader, 
  Typography, 
  Box, 
  Chip, 
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  IconButton,
  Tooltip,
  Skeleton,
  Alert,
} from '@mui/material';
import { 
  Description as DescriptionIcon,
  PlayCircle as PlayIcon,
  Stop as StopIcon,
  RestartAlt as RestartIcon,
  Visibility as ViewIcon,
  TrendingUp,
  Memory,
  Speed,
  Storage,
  Error as ErrorIcon,
  Warning as WarningIcon,
  CheckCircle as CheckIcon,
  Refresh as RefreshIcon,
  Visibility as ViewIcon,
  Business as BusinessIcon,
  Analytics as AnalyticsIcon,
  Settings as SettingsIcon,
} from '@mui/icons-material';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend, AreaChart, Area } from 'recharts';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { usePublisherStore } from '../stores/publisherStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';

const formatUptime = (seconds) => {
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (days > 0) return days + 'd ' + hours + 'h';
  if (hours > 0) return hours + 'h ' + minutes + 'm';
  return minutes + 'm';
};

const LicenseStatusChip = ({ state }) => {
  const colors = {
    Active: { bg: 'success.light', color: 'success.dark' },
    Trial: { bg: 'info.light', color: 'info.dark' },
    Expired: { bg: 'error.light', color: 'error.dark' },
    Revoked: { bg: 'error.light', color: 'error.dark' },
    Pending: { bg: 'warning.light', color: 'warning.dark' },
  };
  const { bg, color } = colors[state] || { bg: 'grey.200', color: 'grey.800' };
  return <Chip label={state} size="small" sx={{ bgcolor: bg, color, fontWeight: 600, fontSize: '0.7rem' }} />;
};

const StatCard = ({ title, value, icon, color, trend, trendUp }) => (
  <Card sx={{ height: '100%' }}>
    <CardContent>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
        <Typography variant="body2" color="text.secondary">{title}</Typography>
        <Box sx={{ bgcolor: theme.palette[color].light + '20', p: 1, borderRadius: 2, color: theme.palette[color].main }}>{icon}</Box>
      </Box>
      <Typography variant="h4" fontWeight={700} sx={{ mb: 0.5 }}>{value}</Typography>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
        <Typography variant="caption" sx={{ color: trendUp ? 'success.main' : 'error.main', fontWeight: 600 }}>{trendUp ? '↑' : '↓'} {trend}</Typography>
        <Typography variant="caption" color="text.secondary">vs last period</Typography>
      </Box>
    </CardContent>
  </Card>
);

const ResourceBar = ({ label, value, max, color, icon }) => (
  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>{icon}<Typography variant="body2" color="text.secondary">{label}</Typography></Box>
      <Typography variant="body2" fontWeight={600} sx={{ color: theme.palette[color].main }}>{value.toFixed(1)}%</Typography>
    </Box>
    <Box sx={{ height: 8, borderRadius: 4, bgcolor: 'grey.200', overflow: 'hidden' }}>
      <Box sx={{ height: '100%', width: Math.min(value / max * 100, 100) + '%', bgcolor: theme.palette[color].main, borderRadius: 4, transition: 'width 0.3s ease' }} />
    </Box>
  </Box>
);

const LicenseStatusChip = ({ state }) => {
  const colors = {
    Active: { bg: 'success.light', color: 'success.dark' },
    Trial: { bg: 'info.light', color: 'info.dark' },
    Expired: { bg: 'error.light', color: 'error.dark' },
    Revoked: { bg: 'error.light', color: 'error.dark' },
    Pending: { bg: 'warning.light', color: 'warning.dark' },
  };
  const { bg, color } = colors[state] || { bg: 'grey.200', color: 'grey.800' };
  return <Chip label={state} size="small" sx={{ bgcolor: bg, color, fontWeight: 600, fontSize: '0.7rem' }} />;
};

export const DashboardPage = () => {
  const queryClient = useQueryClient();
  const { licenses } = useLicenseStore();
  const { publishers } = usePublisherStore();

  const { data: stats, isLoading: statsLoading, error: statsError } = useQuery({
    queryKey: ['dashboardStats'],
    queryFn: () => api.getDashboardStats(),
    refetchInterval: 30000,
    staleTime: 15000,
  });

  const { data: activity, isLoading: activityLoading } = useQuery({
    queryKey: ['recentActivity'],
    queryFn: () => api.getRecentActivity({ limit: 20 }),
    refetchInterval: 30000,
  });

  const { data: metrics, isLoading: metricsLoading } = useQuery({
    queryKey: ['systemMetrics', '1h'],
    queryFn: () => api.getMetrics(undefined, '1h'),
    refetchInterval: 15000,
  });

  const totalLicenses = Object.keys(licenses).length;
  const activeLicenses = Object.values(licenses).filter(l => !l.isRevoked && (!l.expiresAt || new Date(l.expiresAt) > new Date())).length;
  const revokedLicenses = Object.values(licenses).filter(l => l.isRevoked).length;
  const expiredLicenses = Object.values(licenses).filter(l => l.expiresAt && new Date(l.expiresAt) < new Date()).length;

  const activeActivations = Object.values(licenses).reduce((sum, l) => sum + (l.activations?.filter(a => a.isActive).length || 0), 0);
  const totalSeatsAvailable = Object.values(licenses).reduce((sum, l) => sum + l.seatLimit, 0);

  const licenseStats = [
    { title: 'Total Licenses', value: totalLicenses, icon: <DescriptionIcon />, color: 'primary', trend: '+12%', trendUp: true },
    { title: 'Active', value: activeLicenses, icon: <PlayIcon />, color: 'success', trend: '+3', trendUp: true },
    { title: 'Expired', value: expiredLicenses, icon: <WarningIcon />, color: 'warning', trend: '-2', trendUp: false },
    { title: 'Revoked', value: revokedLicenses, icon: <ErrorIcon />, color: 'error', trend: '+1', trendUp: false },
  ];

  if (statsLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}><div style={{width:40,height:40,border:'3px solid #2563EB',borderTop:'3px solid transparent',borderRadius:'50%',animation:'spin 1s linear infinite'}} /></Box>;

  if (statsError) return <Alert severity="error" sx={{ mb: 3 }}>Failed to load dashboard statistics. Please try again later.</Alert>;

  return (
    <Box sx={{ flexGrow: 1 }}>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h4" fontWeight={700} sx={{ mb: 0.5 }}>License Dashboard</Typography>
        <Typography variant="body1" color="text.secondary">Overview of your KKBOT licenses and system health</Typography>
      </Box>

      <Grid container spacing={3} sx={{ mb: 4 }}>
        {licenseStats.map((stat) => (
          <Grid item xs={12} sm={6} md={3} key={stat.title}>
            <StatCard title={stat.title} value={stat.value} icon={stat.icon} color={stat.color} trend={stat.trend} trendUp={stat.trendUp} />
        ))}
      </Grid>

      <Grid container spacing={3} sx={{ mb: 4 }}>
        <Grid item xs={12} lg={8}>
          <Card sx={{ height: '100%' }}>
            <CardHeader 
              title="License Activations (24h)" 
              action={
                <Tooltip title="Refresh">
                  <IconButton size="small" onClick={() => queryClient.invalidateQueries({ queryKey: ['systemMetrics'] })}><RefreshIcon /></IconButton>
                </Tooltip>
              }
            />
            <CardContent>
              {metricsLoading ? <Box sx={{ height: 300, display: 'flex', alignItems: 'center', justifyContent: 'center' }}><div style={{width:40,height:40,border:'3px solid #2563EB',borderTop:'3px solid transparent',borderRadius:'50%',animation:'spin 1s linear infinite'}} /></Box> : (
                <Box sx={{ height: 300 }}>
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={metrics?.history || []} margin={{ top: 10, right: 30, left: 0, bottom: 0 }}>
                      <defs>
                        <linearGradient id="colorActivations" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="5%" stopColor="#2563EB" stopOpacity={0.3}/>
                          <stop offset="95%" stopColor="#2563EB" stopOpacity={0}/>
                        </linearGradient>
                        <linearGradient id="colorRevenue" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="5%" stopColor="#059669" stopOpacity={0.3}/>
                          <stop offset="95%" stopColor="#059669" stopOpacity={0}/>
                        </linearGradient>
                      </defs>
                      <CartesianGrid strokeDasharray="3 3" stroke="#E2E8F0" />
                      <XAxis dataKey="timestamp" tickFormatter={(value) => format(new Date(value), 'HH:mm')} stroke="#94A3B8" fontSize={11} />
                      <YAxis stroke="#94A3B8" fontSize={11} tickFormatter={(value) => value} />
                      <RechartsTooltip 
                        contentStyle={{ backgroundColor: '#1E293B', border: '1px solid #E2E8F0', borderRadius: 8 }}
                        formatter={(value) => [value, '']}
                        labelFormatter={(value) => format(new Date(value), 'HH:mm')}
                      />
                      <Legend />
                      <Area 
                        type="monotone" 
                        dataKey="activations" 
                        stroke="#2563EB" 
                        fillOpacity={1} 
                        fill="url(#colorActivations)" 
                        name="Activations" 
                      />
                      <Area 
                        type="monotone" 
                        dataKey="revenue" 
                        stroke="#059669" 
                        fillOpacity={1} 
                        fill="url(#colorRevenue)" 
                        name="Revenue" 
                      />
                    </AreaChart>
                  </ResponsiveContainer>
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} lg={4}>
          <Card sx={{ height: '100%' }}>
            <CardHeader title="System Resources" />
            <CardContent>
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                <ResourceBar label="CPU Usage" value={metrics?.current?.cpu || 0} max={100} color="primary" icon={<SpeedIcon />} />
                <ResourceBar label="Memory Usage" value={metrics?.current?.memory || 0} max={100} color="success" icon={<MemoryIcon />} />
                <ResourceBar label="Disk Usage" value={metrics?.current?.disk || 0} max={100} color="warning" icon={<StorageIcon />} />
                <ResourceBar label="Network I/O" value={metrics?.current?.network || 0} max={100} color="info" icon={<TrendingUp />} />
              </Box>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Grid container spacing={3}>
        <Grid item xs={12}>
          <Card>
            <CardHeader 
              title="Recent Licenses" 
              action={
                <Button variant="outlined" size="small" startIcon={<ViewIcon />}>
                  View All
                </Button>
              }
            />
            <CardContent>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>License</TableCell>
                      <TableCell>Key</TableCell>
                      <TableCell>Tier</TableCell>
                      <TableCell align="center">Status</TableCell>
                      <TableCell align="center">Seats Used</TableCell>
                      <TableCell align="center">Expires</TableCell>
                      <TableCell align="right">Actions</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {Object.values(licenses).slice(0, 10).map((license) => (
                      <TableRow key={license.id} hover>
                        <TableCell>
                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <DescriptionIcon sx={{ fontSize: 20, color: 'text.secondary' }} />
                            <Typography variant="body2" fontWeight={500}>{license.name}</Typography>
                          </Box>
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" color="text.secondary" sx={{ fontFamily: 'monospace' }}>{license.key}</Typography>
                        </TableCell>
                        <TableCell>
                          <Chip 
                            label={license.tier} 
                            size="small" 
                            variant={license.tier === 'Trial' ? 'outlined' : 'filled'} 
                            color={license.tier === 'Trial' ? 'default' : license.tier === 'Enterprise' ? 'warning' : 'primary'} 
                          />
                        </TableCell>
                        <TableCell align="center">
                          <LicenseStatusChip state={license.isRevoked ? 'Revoked' : license.expiresAt && new Date(license.expiresAt) < new Date() ? 'Expired' : 'Active'} />
                        </TableCell>
                        <TableCell align="center">
                          <Typography variant="body2">{license.activations?.filter(a => a.isActive).length || 0} / {license.seatLimit}</Typography>
                        </TableCell>
                        <TableCell align="center">
                          <Typography variant="body2" color="text.secondary">
                            {license.expiresAt ? format(new Date(license.expiresAt), 'yyyy-MM-dd') : 'Perpetual'}
                          </Typography>
                        </TableCell>
                        <TableCell align="right">
                          <Tooltip title="View Details"><IconButton size="small" onClick={() => navigate('/licenses/' + license.id)}><ViewIcon /></IconButton></Tooltip>
                          <Tooltip title="Edit"><IconButton size="small" onClick={() => navigate('/licenses/' + license.id)}><EditIcon fontSize="small" /></IconButton></Tooltip>
                          <Tooltip title="Revoke"><IconButton size="small" color="error" onClick={() => revokeMutation.mutate(license.key)} disabled={revokeMutation.isPending}><DeleteIcon fontSize="small" /></IconButton></Tooltip>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
};

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { usePublisherStore } from '../stores/publisherStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';
import { Grid, Card, CardContent, CardHeader, Typography, Box, Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, IconButton, Tooltip, Alert } from '@mui/material';
import { Description as DescriptionIcon, PlayCircle as PlayIcon, Stop as StopIcon, RestartAlt as RestartIcon, Visibility as ViewIcon, TrendingUp, Memory, Speed, Storage, Error as ErrorIcon, Warning as WarningIcon, CheckCircle as CheckIcon, Refresh as RefreshIcon, Visibility as ViewIcon, Edit as EditIcon, Delete as DeleteIcon } from '@mui/icons-material';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend, AreaChart, Area } from 'recharts';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { usePublisherStore } from '../stores/publisherStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';

export default DashboardPage;
