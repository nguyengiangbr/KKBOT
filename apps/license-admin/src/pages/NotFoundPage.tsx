import React from 'react';
import { Box, Typography, Button, Link } from '@mui/material';
import { Home as HomeIcon, Search as SearchIcon } from '@mui/icons-material';
import { useTheme } from '@mui/material/styles';
import { navigate } from '../navigation/rootNavigation';

export const NotFoundPage = () => {
  const theme = useTheme();

  return (
    <Box 
      sx={{ 
        display: 'flex', 
        flexDirection: 'column', 
        alignItems: 'center', 
        justifyContent: 'center', 
        minHeight: '60vh',
        textAlign: 'center',
        px: 3,
      }}
    >
      <Typography variant="h1" fontWeight={700} sx={{ mb: 2, color: 'primary.main' }}>
        404
      </Typography>
      <Typography variant="h5" sx={{ mb: 3, color: 'text.secondary' }}>
        Page Not Found
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 4, maxWidth: 400 }}>
        Sorry, we couldn't find the page you're looking for. It might have been moved or doesn't exist.
      </Typography>
      <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center', flexWrap: 'wrap' }}>
        <Button variant="contained" startIcon={<HomeIcon />} onClick={() => navigate('/')}>
          Go Home
        </Button>
        <Button variant="outlined" startIcon={<SearchIcon />} onClick={() => navigate('/licenses')}>
          Browse Licenses
        </Button>
      </Box>
    </Box>
  );
};

export default NotFoundPage;
