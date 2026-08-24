import React from 'react';
import { 
  Grid, 
  Card, 
  CardContent, 
  CardHeader, 
  Typography, 
  Box, 
  Button, 
  Chip, 
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  IconButton,
  Tooltip,
  Searchbar,
  TextField,
  Menu,
  MenuItem,
  Divider,
  Alert,
} from '@mui/material';
import { 
  Description as DescriptionIcon,
  PlayCircle as PlayIcon,
  Stop as StopIcon,
  RestartAlt as RestartIcon,
  Visibility as ViewIcon,
  Edit as EditIcon,
  Delete as DeleteIcon,
  Add as AddIcon,
  FilterList as FilterIcon,
  Download as DownloadIcon,
  Refresh as RefreshIcon,
} from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';

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

export const LicensesPage = () => {
  const [refreshing, setRefreshing] = React.useState(false);
  const [searchQuery, setSearchQuery] = React.useState('');
  const [tierFilter, setTierFilter] = React.useState('all');
  const [stateFilter, setStateFilter] = React.useState('all');
  const queryClient = useQueryClient();

  const { data: licenses, isLoading, error } = useQuery({
    queryKey: ['licenses', searchQuery, tierFilter, stateFilter],
    queryFn: () => api.getLicenses({ search: searchQuery, tier: tierFilter !== 'all' ? tierFilter : undefined, state: stateFilter !== 'all' ? stateFilter : undefined }),
    refetchInterval: 30000,
    staleTime: 10000,
  });

  const createMutation = useMutation({
    mutationFn: (data) => api.createLicense(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['licenses'] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }) => api.updateLicense(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['licenses'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id) => api.deleteLicense(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['licenses'] });
    },
  });

  const revokeMutation = useMutation({
    mutationFn: ({ id, reason }) => api.revokeLicense(id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['licenses'] });
    },
  });

  const onRefresh = async () => {
    setRefreshing(true);
    await queryClient.invalidateQueries({ queryKey: ['licenses'] });
    setRefreshing(false);
  };

  const filteredLicenses = Object.values(licenses || {}).filter(license => {
    if (searchQuery) {
      const search = searchQuery.toLowerCase();
      if (!license.name.toLowerCase().includes(search) &&
          !license.key.toLowerCase().includes(search) &&
          !license.description?.toLowerCase().includes(search)) {
        return false;
      }
    }
    if (tierFilter !== 'all' && license.tier !== tierFilter) {
      return false;
    }
    if (stateFilter !== 'all') {
      if (stateFilter === 'revoked' && !license.isRevoked) return false;
      if (stateFilter === 'expired' && (!license.expiresAt || new Date(license.expiresAt) > new Date())) return false;
      if (stateFilter === 'active' && (license.isRevoked || (license.expiresAt && new Date(license.expiresAt) < new Date()))) return false;
    }
    return true;
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}>
        <div style={{width:40,height:40,border:'3px solid #2563EB',borderTop:'3px solid transparent',borderRadius:'50%',animation:'spin 1s linear infinite'}} />
        <Typography variant="body2" color="text.secondary" sx={{ml: 2}}>Loading licenses...</Typography>
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
        Failed to load licenses. Please try again later.
      </Alert>
    );
  }

  return (
    <Box sx={{ flexGrow: 1 }}>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2 }}>
        <Box>
          <Typography variant="h4" fontWeight={700} sx={{ mb: 0.5 }}>Licenses</Typography>
          <Typography variant="body1" color="text.secondary">Manage your KKBOT licenses</Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
          <Button 
            variant="outlined" 
            startIcon={<RefreshIcon />} 
            onClick={onRefresh} 
            disabled={refreshing}
            sx={{ minWidth: 120 }}
          >
            {refreshing ? 'Refreshing...' : 'Refresh'}
          </Button>
          <Button 
            variant="contained" 
            startIcon={<AddIcon />} 
            onClick={() => navigate('/licenses/new')}
            sx={{ minWidth: 140 }}
          >
            Create License
          </Button>
        </Box>
      </Box>

      <Box sx={{ mb: 3, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <Searchbar
          placeholder="Search licenses..."
          onChange={(e) => setSearchQuery(e.target.value)}
          value={searchQuery}
          style={{ minWidth: 300, flex: 1 }}
        />
        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          <TextField
            select
            value={tierFilter}
            label="Tier"
            onChange={(e) => setTierFilter(e.target.value)}
            SelectProps={{ native: true }}
            sx={{ minWidth: 150 }}
          >
            <option value="all">All Tiers</option>
            <option value="Trial">Trial</option>
            <option value="Basic">Basic</option>
            <option value="Pro">Pro</option>
            <option value="Enterprise">Enterprise</option>
          </TextField>
          <TextField
            select
            value={stateFilter}
            label="Status"
            onChange={(e) => setStateFilter(e.target.value)}
            SelectProps={{ native: true }}
            sx={{ minWidth: 150 }}
          >
            <option value="all">All Status</option>
            <option value="active">Active</option>
            <option value="expired">Expired</option>
            <option value="revoked">Revoked</option>
          </TextField>
        </Box>
      </Box>

      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>License</TableCell>
              <TableCell>Key</TableCell>
              <TableCell>Tier</TableCell>
              <TableCell align="center">Status</TableCell>
              <TableCell align="center">Seats</TableCell>
              <TableCell align="center">Expires</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {licenses?.slice(0, 50).map((license) => (
              <TableRow key={license.id} hover>
                <TableCell>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <DescriptionIcon sx={{ fontSize: 20, color: 'text.secondary' }} />
                    <Box>
                      <Typography variant="body2" fontWeight={500}>{license.name}</Typography>
                      <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>{license.key}</Typography>
                    </Box>
                  </Box>
                </TableCell>
                <TableCell>
                  <Typography variant="body2" color="text.secondary" sx={{ fontFamily: 'monospace', fontSize: '0.75rem' }}>{license.key}</Typography>
                </TableCell>
                <TableCell>
                  <TierChip tier={license.tier} />
                </TableCell>
                <TableCell align="center">
                  <LicenseStatusChip state={license.isRevoked ? 'Revoked' : license.expiresAt && new Date(license.expiresAt) < new Date() ? 'Expired' : 'Active'} />
                </TableCell>
                <TableCell align="center">
                  <Typography variant="body2">
                    {license.activations?.filter(a => a.isActive).length || 0} / {license.seatLimit}
                  </Typography>
                </TableCell>
                <TableCell align="center">
                  <Typography variant="body2" color="text.secondary">
                    {license.expiresAt ? format(new Date(license.expiresAt), 'yyyy-MM-dd') : 'Perpetual'}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  <Tooltip title="View Details"><IconButton size="small" onClick={() => navigate('/licenses/' + license.id)}><ViewIcon /></IconButton></Tooltip>
                  <Tooltip title="Edit"><IconButton size="small" onClick={() => navigate('/licenses/' + license.id)}><EditIcon fontSize="small" /></IconButton></Tooltip>
                  <Tooltip title="Revoke"><IconButton size="small" color="error" onClick={() => revokeMutation.mutate({ id: license.id, reason: 'Revoked by admin' })} disabled={revokeMutation.isPending}><DeleteIcon fontSize="small" /></IconButton></Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
};

import React from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';
import { Grid, Card, CardContent, CardHeader, Typography, Box, Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, IconButton, Tooltip, Searchbar, TextField, Menu, MenuItem, Divider, Alert } from '@mui/material';
import { Description as DescriptionIcon, PlayCircle as PlayIcon, Stop as StopIcon, RestartAlt as RestartIcon, Visibility as ViewIcon, Edit as EditIcon, Delete as DeleteIcon, Add as AddIcon, FilterList as FilterIcon, Download as DownloadIcon, Refresh as RefreshIcon } from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { useLicenseStore } from '../stores/licenseStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';

export default LicensesPage;
