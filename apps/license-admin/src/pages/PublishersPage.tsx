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
  Business as BusinessIcon,
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
  VerifiedUser as VerifiedIcon,
  Person as PersonIcon,
} from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { usePublisherStore } from '../stores/publisherStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';

const VerifiedChip = ({ verified }) => {
  if (verified) {
    return <Chip label="Verified" icon={<VerifiedIcon />} size="small" color="success" variant="outlined" />;
  }
  return <Chip label="Unverified" size="small" color="default" variant="outlined" />;
};

export const PublishersPage = () => {
  const [refreshing, setRefreshing] = React.useState(false);
  const [searchQuery, setSearchQuery] = React.useState('');
  const [verifiedFilter, setVerifiedFilter] = React.useState('all');
  const queryClient = useQueryClient();

  const { data: publishers, isLoading, error } = useQuery({
    queryKey: ['publishers', searchQuery, verifiedFilter],
    queryFn: () => api.getPublishers({ search: searchQuery, verified: verifiedFilter !== 'all' ? verifiedFilter === 'verified' : undefined }),
    refetchInterval: 30000,
    staleTime: 10000,
  });

  const createMutation = useMutation({
    mutationFn: (data) => api.createPublisher(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['publishers'] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }) => api.updatePublisher(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['publishers'] });
    },
  );

  const deleteMutation = useMutation({
    mutationFn: (id) => api.deletePublisher(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['publishers'] });
    },
  );

  const verifyMutation = useMutation({
    mutationFn: (id) => api.verifyPublisher(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['publishers'] });
    },
  });

  const onRefresh = async () => {
    setRefreshing(true);
    await queryClient.invalidateQueries({ queryKey: ['publishers'] });
    setRefreshing(false);
  };

  const filteredPublishers = Object.values(publishers || {}).filter(publisher => {
    if (searchQuery) {
      const search = searchQuery.toLowerCase();
      if (!publisher.name.toLowerCase().includes(search) &&
          !publisher.email.toLowerCase().includes(search)) {
        return false;
      }
    }
    if (verifiedFilter !== 'all') {
      if (verifiedFilter === 'verified' && !publisher.isVerified) return false;
      if (verifiedFilter === 'unverified' && publisher.isVerified) return false;
    }
    return true;
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}>
        <div style={{width:40,height:40,border:'3px solid #2563EB',borderTop:'3px solid transparent',borderRadius:'50%',animation:'spin 1s linear infinite'}} />
        <Typography variant="body2" color="text.secondary" sx={{ml: 2}}>Loading publishers...</Typography>
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
        Failed to load publishers. Please try again later.
      </Alert>
    );
  }

  return (
    <Box sx={{ flexGrow: 1 }}>
      <Box sx={{ mb: 3, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2 }}>
        <Box>
          <Typography variant="h4" fontWeight={700} sx={{ mb: 0.5 }}>Publishers</Typography>
          <Typography variant="body1" color="text.secondary">Manage license publishers</Typography>
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
            onClick={() => navigate('/publishers/new')}
            sx={{ minWidth: 140 }}
          >
            Add Publisher
          </Button>
        </Box>
      </Box>

      <Box sx={{ mb: 3, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <Searchbar
          placeholder="Search publishers..."
          onChange={(e) => setSearchQuery(e.target.value)}
          value={searchQuery}
          style={{ minWidth: 300, flex: 1 }}
        />
        <TextField
          select
          value={verifiedFilter}
          label="Verification"
          onChange={(e) => setVerifiedFilter(e.target.value)}
          SelectProps={{ native: true }}
          sx={{ minWidth: 200 }}
        >
          <option value="all">All</option>
          <option value="verified">Verified</option>
          <option value="unverified">Unverified</option>
        </TextField>
      </Box>

      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Publisher</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Verification</TableCell>
              <TableCell align="center">Licenses</TableCell>
              <TableCell align="center">Revenue Share</TableCell>
              <TableCell align="center">Created</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {publishers?.slice(0, 50).map((publisher) => (
              <TableRow key={publisher.id} hover>
                <TableCell>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <PersonIcon sx={{ fontSize: 20, color: 'text.secondary' }} />
                    <Box>
                      <Typography variant="body2" fontWeight={500}>{publisher.name}</Typography>
                      <Typography variant="caption" color="text.secondary">{publisher.id}</Typography>
                    </Box>
                  </Box>
                </TableCell>
                <TableCell>
                  <Typography variant="body2" color="text.secondary">{publisher.email}</Typography>
                </TableCell>
                <TableCell>
                  <VerifiedChip verified={publisher.isVerified} />
                </TableCell>
                <TableCell align="center">
                  <Typography variant="body2">{publisher.licenseCount || 0}</Typography>
                </TableCell>
                <TableCell align="center">
                  <Typography variant="body2">{publisher.revenueSharePercent}%</Typography>
                </TableCell>
                <TableCell align="center">
                  <Typography variant="body2" color="text.secondary">
                    {format(new Date(publisher.createdAt), 'yyyy-MM-dd')}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  <Tooltip title="View Details"><IconButton size="small" onClick={() => navigate('/publishers/' + publisher.id)}><ViewIcon /></IconButton></Tooltip>
                  <Tooltip title="Edit"><IconButton size="small" onClick={() => navigate('/publishers/' + publisher.id)}><EditIcon fontSize="small" /></IconButton></Tooltip>
                  {!publisher.isVerified && (
                    <Tooltip title="Verify Publisher"><IconButton size="small" color="success" onClick={() => verifyMutation.mutate(publisher.id)} disabled={verifyMutation.isPending}><VerifiedIcon fontSize="small" /></IconButton></Tooltip>
                  )}
                  <Tooltip title="Delete"><IconButton size="small" color="error" onClick={() => deleteMutation.mutate(publisher.id)} disabled={deleteMutation.isPending}><DeleteIcon fontSize="small" /></IconButton></Tooltip>
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
import { usePublisherStore } from '../stores/publisherStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';
import { Grid, Card, CardContent, CardHeader, Typography, Box, Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, IconButton, Tooltip, Searchbar, TextField, Menu, MenuItem, Divider, Alert } from '@mui/material';
import { Business as BusinessIcon, PlayCircle as PlayIcon, Stop as StopIcon, RestartAlt as RestartIcon, Visibility as ViewIcon, Edit as EditIcon, Delete as DeleteIcon, Add as AddIcon, FilterList as FilterIcon, Download as DownloadIcon, Refresh as RefreshIcon, VerifiedUser as VerifiedIcon, Person as PersonIcon } from '@mui/icons-material';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { formatDistanceToNow, format } from 'date-fns';
import { api } from '../services/api';
import { usePublisherStore } from '../stores/publisherStore';
import { theme } from '../theme';
import { navigate } from '../navigation/rootNavigation';

export default PublishersPage;
