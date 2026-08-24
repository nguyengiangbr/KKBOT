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
  Tabs,
  Tab,
  Divider,
  Alert,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
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
  Edit as EditIcon,
  Delete as DeleteIcon,
  Copy as CopyIcon,
} from '@mui/icons-material';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend, AreaChart, Area } from 'recharts';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { theme } from '../theme';
import { navigate, useParams } from '../navigation/rootNavigation';

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

const TierChip = ({ tier }) => {
  const colors = {
    Trial: { bg: 'info.light', color: 'info.dark' },
    Basic: { bg: 'primary.light', color: 'primary.dark' },
    Pro: { bg: 'warning.light', color: 'warning.dark' },
    Enterprise: { bg: 'warning.main', color: 'white' },
  };
  const { bg, color } = colors[tier] || { bg: 'grey.200', color: 'grey.800' };
  return <Chip label={tier} size="small" sx={{ bgcolor: bg, color, fontWeight: 600, fontSize: '0.7rem' }} />;
};

export const LicenseDetailPage = () => {
  const { id } = useParams();
  const queryClient = useQueryClient();

  const { data: license, isLoading, error } = useQuery({
    queryKey: ['license', id],
    queryFn: () => api.getLicense(id),
    staleTime: 15000,
  });

  const { data: activations, isLoading: activationsLoading } = useQuery({
    queryKey: ['activations', id],
    queryFn: () => api.getActivations(id),
    refetchInterval: 15000,
    enabled: !!id,
  });

  const { data: metrics, isLoading: metricsLoading } = useQuery({
    queryKey: ['licenseMetrics', id, '1h'],
    queryFn: () => api.getLicenseMetrics(id, '1h'),
    refetchInterval: 15000,
    enabled: !!id,
  });

  const updateMutation = useMutation({
    mutationFn: (data) => api.updateLicense(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['license', id] });
      queryClient.invalidateQueries({ queryKey: ['licenses'] });
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (reason) => api.revokeLicense(id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['license', id] });
      queryClient.invalidateQueries({ queryKey: ['licenses'] });
    },
  });

  const [editDialogOpen, setEditDialogOpen] = React.useState(false);
  const [editForm, setEditForm] = React.useState({
    name: '',
    description: '',
    tier: '',
    seatLimit: 1,
    expiresAt: '',
  });

  if (!license) {
    if (isLoading) {
      return <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}><div style={{width:40,height:40,border:'3px solid #2563EB',borderTop:'3px solid transparent',borderRadius:'50%',animation:'spin 1s linear infinite'}} /></Box>;
    }
    if (error) {
      return (
        <Alert severity="error" sx={{ mb: 3 }}>
          License not found. <Button onClick={() => navigate('/licenses')}>Back to licenses</Button>
        </Alert>
      );
    }
    return null;
  }

  const handleEditSubmit = async (e) => {
    e.preventDefault();
    try {
      await updateMutation.mutateAsync(editForm);
      setEditDialogOpen(false);
    } catch (err) {
      // Error handled by mutation
    }
  };

  const handleRevoke = async () => {
    const reason = window.prompt('Enter revocation reason:');
    if (reason) {
      await revokeMutation.mutateAsync(reason);
    }
  };

  const openEditDialog = () => {
    setEditForm({
      name: license.name,
      description: license.description || '',
      tier: license.tier,
      seatLimit: license.seatLimit,
      expiresAt: license.expiresAt ? new Date(license.expiresAt).toISOString().slice(0, 16) : '',
    });
    setEditDialogOpen(true);
  };

  const runningInstances = license.activations?.filter(a => a.isActive).length || 0;
  const stoppedInstances = license.activations?.filter(a => !a.isActive).length || 0;

  const statCards = [
    { title: 'Total Seats', value: license.seatLimit, icon: 'server', color: 'primary', trend: '', trendUp: true },
    { title: 'Active', value: runningInstances, icon: 'play_circle', color: 'success', trend: '', trendUp: true },
    { title: 'Stopped', value: stoppedInstances, icon: 'stop', color: 'warning', trend: '', trendUp: false },
    { title: 'Available', value: license.seatLimit - runningInstances, icon: 'check_circle', color: 'info', trend: '', trendUp: true },
  ];

  if (!license) {
    if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}><div style={{width:40,height:40,border:'3px solid #2563EB',borderTop:'3px solid transparent',borderRadius:'50%',animation:'spin 1s linear infinite'}} /></Box>;
    if (error) return <Alert severity="error" sx={{ mb: 3 }}>License not found. <Button onClick={() => navigate('/licenses')}>Back to licenses</Button></Alert>;
    return null;
  }

  return (
    <Box sx={{ flexGrow: 1 }}>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2 }}>
        <Box>
          <Typography variant="h4" fontWeight={700} sx={{ mb: 0.5, display: 'flex', alignItems: 'center', gap: 1 }}>
            {license.name}
            <TierChip tier={license.tier} />
            <LicenseStatusChip state={license.isRevoked ? 'Revoked' : license.expiresAt && new Date(license.expiresAt) < new Date() ? 'Expired' : 'Active'} />
          </Typography>
          <Typography variant="body1" color="text.secondary">{license.description || 'No description'}</Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', flexWrap: 'wrap' }}>
          <Button variant="outlined" startIcon={<RefreshIcon />} onClick={() => queryClient.invalidateQueries({ queryKey: ['license', id] })}>Refresh</Button>
          <Button variant="outlined" startIcon={<EditIcon />} onClick={openEditDialog}>Edit</Button>
          <Button variant="outlined" color="error" startIcon={<DeleteIcon />} onClick={handleRevoke}>Revoke</Button>
          <Button variant="text" onClick={() => navigate('/licenses')}>Back to List</Button>
        </Box>
      </Box>

      <Grid container spacing={3} sx={{ mb: 4 }}>
        {statCards.map((stat) => (
          <Grid item xs={12} sm={6} md={3} key={stat.title}>
            <Card sx={{ height: '100%' }}>
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
                  <Typography variant="body2" color="text.secondary">{stat.title}</Typography>
                  <Box sx={{ bgcolor: theme.palette[stat.color].light + '20', p: 1, borderRadius: 2, color: theme.palette[stat.color].main }}>{stat.icon}</Box>
                </Box>
                <Typography variant="h4" fontWeight={700} sx={{ mb: 0.5 }}>{stat.value}</Typography>
              </CardContent>
            </Card>
        ))}
      </Grid>

      <Grid container spacing={3} sx={{ mb: 4 }}>
        <Grid item xs={12} lg={8}>
          <Card sx={{ height: '100%' }}>
            <CardHeader 
              title="Activations Timeline (24h)" 
              action={
                <Tooltip title="Refresh">
                  <IconButton size="small" onClick={() => queryClient.invalidateQueries({ queryKey: ['licenseMetrics', id] })}><RefreshIcon /></IconButton>
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
                        <linearGradient id="colorDeactivations" x1="0" y1="0" x2="0" y2="1">
                          <stop offset="5%" stopColor="#EF4444" stopOpacity={0.3}/>
                          <stop offset="95%" stopColor="#EF4444" stopOpacity={0}/>
                        </linearGradient>
                      </defs>
                      <CartesianGrid strokeDasharray="3 3" stroke="#E2E8F0" />
                      <XAxis dataKey="timestamp" tickFormatter={(value) => format(new Date(value), 'HH:mm')} stroke="#94A3B8" fontSize={11} />
                      <YAxis stroke="#94A3B8" fontSize={11} />
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
                        dataKey="deactivations" 
                        stroke="#EF4444" 
                        fillOpacity={1} 
                        fill="url(#colorDeactivations)" 
                        name="Deactivations" 
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
            <CardHeader title="Activation Details" />
            <CardContent>
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
                <Box>
                  <Typography variant="body2" color="text.secondary">Total Seats</Typography>
                  <Typography variant="h5" fontWeight={700}>{license.seatLimit}</Typography>
                </Box>
                <Divider />
                <Box>
                  <Typography variant="body2" color="text.secondary">Used Seats</Typography>
                  <Typography variant="h5" fontWeight={700} color="success.main">{runningInstances}</Typography>
                </Box>
                <Divider />
                <Box>
                  <Typography variant="body2" color="text.secondary">Available Seats</Typography>
                  <Typography variant="h5" fontWeight={700} color="info.main">{license.seatLimit - runningInstances}</Typography>
                </Box>
                <Divider />
                <Box>
                  <Typography variant="body2" color="text.secondary">Utilization</Typography>
                  <Typography variant="h5" fontWeight={700} color={license.seatLimit > 0 && runningInstances / license.seatLimit > 0.8 ? 'warning.main' : 'success.main'}>
                    {license.seatLimit > 0 ? Math.round(runningInstances / license.seatLimit * 100) : 0}%
                  </Typography>
                </Box>
              </Box>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Grid container spacing={3}>
        <Grid item xs={12}>
          <Card>
            <CardHeader 
              title="Activations" 
              action={
                <Button variant="outlined" size="small" startIcon={<AddIcon />}>
                  Manual Activate
                </Button>
              }
            />
            <CardContent>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Device</TableCell>
                      <TableCell>Machine</TableCell>
                      <TableCell>OS</TableCell>
                      <TableCell align="center">State</TableCell>
                      <TableCell align="center">Last Heartbeat</TableCell>
                      <TableCell align="center">Version</TableCell>
                      <TableCell align="right">Actions</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {activations?.map((activation) => (
                      <TableRow key={activation.id} hover>
                        <TableCell>
                          <Typography variant="body2" color="text.secondary" sx={{ fontFamily: 'monospace', fontSize: '0.75rem' }}>{activation.hardwareId.slice(0, 16)}...</Typography>
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" color="text.secondary">{activation.machineName || 'Unknown'}</Typography>
                        </TableCell>
                        <TableCell>
                          <Typography variant="body2" color="text.secondary">{activation.osVersion || 'Unknown'}</Typography>
                        </TableCell>
                        <TableCell align="center">
                          <LicenseStatusChip state={activation.isActive ? 'Active' : 'Stopped'} />
                        </TableCell>
                        <TableCell align="center">
                          <Typography variant="body2" color="text.secondary">{formatDistanceToNow(new Date(activation.lastHeartbeatAt), { addSuffix: true })}</Typography>
                        </TableCell>
                        <TableCell align="center">
                          <Typography variant="body2" color="text.secondary">{activation.clientVersion || 'Unknown'}</Typography>
                        </TableCell>
                        <TableCell align="right">
                          {activation.isActive ? (
                            <Tooltip title="Deactivate"><IconButton size="small" onClick={() => deactivateMutation.mutate({ licenseId: license.id, hardwareId: activation.hardwareId })} disabled={deactivateMutation.isPending}><StopIcon fontSize="small" /></IconButton></Tooltip>
                          ) : (
                            <Tooltip title="Activate"><IconButton size="small" onClick={() => activateMutation.mutate({ licenseId: license.id, hardwareId: activation.hardwareId })} disabled={activateMutation.isPending}><PlayIcon fontSize="small" /></IconButton></Tooltip>
                          )}
                          <Tooltip title="Remove"><IconButton size="small" color="error" onClick={() => deleteActivationMutation.mutate({ licenseId: license.id, hardwareId: activation.hardwareId })} disabled={deleteActivationMutation.isPending}><DeleteIcon fontSize="small" /></IconButton></Tooltip>
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

      {/* Edit Dialog */}
      <Dialog open={editDialogOpen} onClose={() => setEditDialogOpen(false)} maxWidth="md" fullWidth>
        <form onSubmit={handleEditSubmit}>
          <DialogTitle>Edit License</DialogTitle>
          <DialogContent>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
              <TextField
                fullWidth
                label="Name"
                value={editForm.name}
                onChange={(e) => setEditForm({...editForm, name: e.target.value})}
                required
              />
              <TextField
                fullWidth
                label="Description"
                value={editForm.description}
                onChange={(e) => setEditForm({...editForm, description: e.target.value})}
                multiline
                rows={3}
              />
              <FormControl fullWidth>
                <InputLabel id="tier-label">Tier</InputLabel>
                <Select
                  labelId="tier-label"
                  value={editForm.tier}
                  label="Tier"
                  onChange={(e) => setEditForm({...editForm, tier: e.target.value})}
                >
                  <MenuItem value="Trial">Trial</MenuItem>
                  <MenuItem value="Basic">Basic</MenuItem>
                  <MenuItem value="Pro">Pro</MenuItem>
                  <MenuItem value="Enterprise">Enterprise</MenuItem>
                </Select>
              </FormControl>
              <TextField
                type="number"
                fullWidth
                label="Seat Limit"
                value={editForm.seatLimit}
                onChange={(e) => setEditForm({...editForm, seatLimit: parseInt(e.target.value) || 1})}
                inputProps={{ min: 1, max: 10000 }}
              />
              <TextField
                type="datetime-local"
                fullWidth
                label="Expires At (Optional)"
                value={editForm.expiresAt}
                onChange={(e) => setEditForm({...editForm, expiresAt: e.target.value})}
                InputLabelProps={{ shrink: true }}
              />
            </Box>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setEditDialogOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">Save Changes</Button>
          </DialogActions>
        </form>
      </Dialog>
    </Box>
  );
};

import React from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';
import { Grid, Card, CardContent, CardHeader, Typography, Box, Chip, Button, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, IconButton, Tooltip, Tabs, Tab, Divider, Alert, Dialog, DialogTitle, DialogContent, DialogActions, TextField, FormControl, InputLabel, Select, MenuItem } from '@mui/material';
import { Description as DescriptionIcon, PlayCircle as PlayIcon, Stop as StopIcon, RestartAlt as RestartIcon, Visibility as ViewIcon, TrendingUp, Memory, Speed, Storage, Error as ErrorIcon, Warning as WarningIcon, CheckCircle as CheckIcon, Refresh as RefreshIcon, Visibility as ViewIcon, Edit as EditIcon, Delete as DeleteIcon, Copy as CopyIcon } from '@mui/icons-material';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend, AreaChart, Area } from 'recharts';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';

export default LicenseDetailPage;
