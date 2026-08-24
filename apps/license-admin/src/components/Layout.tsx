import React from 'react';
import { 
  Drawer, 
  AppBar, 
  Toolbar, 
  List, 
  ListItem, 
  ListItemIcon, 
  ListItemText, 
  Box, 
  Typography,
  IconButton,
  Avatar,
  Menu,
  MenuItem,
  Divider,
  useMediaQuery,
  useTheme,
  Collapse,
  Tooltip,
  Badge
} from '@mui/material';
import { 
  Menu as MenuIcon,
  Dashboard as DashboardIcon,
  Description as DescriptionIcon,
  Business as BusinessIcon,
  Analytics as AnalyticsIcon,
  Settings as SettingsIcon,
  ChevronLeft as ChevronLeftIcon,
  ChevronRight as ChevronRightIcon,
  AccountCircle,
  Logout,
  Person,
  Help,
  Brightness4,
  Notifications,
  Add,
  Refresh,
} from '@mui/icons-material';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../stores/authStore';
import { useLicenseStore } from '../stores/licenseStore';
import { socket } from '../services/socket';
import { useState } from 'react';

const drawerWidth = 260;
const collapsedDrawerWidth = 72;

const navigationItems = [
  { path: '/', label: 'Dashboard', icon: <DashboardIcon /> },
  { path: '/licenses', label: 'Licenses', icon: <DescriptionIcon /> },
  { path: '/publishers', label: 'Publishers', icon: <BusinessIcon /> },
  { path: '/analytics', label: 'Analytics', icon: <AnalyticsIcon /> },
  { path: '/settings', label: 'Settings', icon: <SettingsIcon /> },
];

export const Layout = () => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const location = useLocation();
  const navigate = useNavigate();
  const { user, isAuthenticated, logout } = useAuth();
  const { licenses } = useLicenseStore();

  const handleDrawerToggle = () => {
    if (isMobile) {
      setMobileOpen(!mobileOpen);
    } else {
      setCollapsed(!collapsed);
    }
  };

  const handleDrawerClose = () => {
    if (isMobile) {
      setMobileOpen(false);
    }
  };

  const handleProfileMenuOpen = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleProfileMenuClose = () => {
    setAnchorEl(null);
  };

  const handleLogout = async () => {
    handleProfileMenuClose();
    logout();
  };

  const drawerContent = (
    <Box sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column' }}>
      <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider' }}>
        {!collapsed && (
          <Typography variant="h6" sx={{ fontWeight: 700, color: 'primary.main', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            KKBOT License Admin
          </Typography>
        )}
        {collapsed && <Typography variant="h6" sx={{ fontWeight: 700, color: 'primary.main', textAlign: 'center' }}>K</Typography>}
      </Box>

      <List sx={{ flex: 1, px: 1, py: 1 }}>
        {navigationItems.map((item) => {
          const isActive = location.pathname === item.path || 
            (item.path !== '/' && location.pathname.startsWith(item.path + '/'));
          
          return (
            <Tooltip 
              key={item.path} 
              title={collapsed ? item.label : ''} 
              placement="right"
              disableHoverListener={!collapsed}
            >
              <NavLink
                to={item.path}
                onClick={handleDrawerClose}
                style={({ isActive }) => ({
                  borderRadius: 2,
                  margin: '4px 0',
                  backgroundColor: isActive ? 'primary.light' : 'transparent',
                  color: isActive ? 'primary.contrastText' : 'text.primary',
                  '& .MuiListItemIcon-root': {
                    color: isActive ? 'primary.contrastText' : 'text.secondary',
                    minWidth: 40,
                  },
                  '& .MuiListItemText-primary': {
                    fontWeight: isActive ? 600 : 400,
                  },
                }}
              >
                <ListItemIcon sx={{ minWidth: 40 }}>{item.icon}</ListItemIcon>
                {!collapsed && <ListItemText primary={item.label} />}
              </NavLink>
            </Tooltip>
          );
        })}
      </List>

      {!collapsed && (
        <Divider sx={{ my: 1 }} />
      )}

      {!collapsed && (
        <Box sx={{ p: 2 }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            QUICK ACTIONS
          </Typography>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            <Tooltip title="Create License">
              <NavLink to="/licenses" onClick={handleDrawerClose}>
                <Button startIcon={<Add />} variant="outlined" size="small" fullWidth sx={{ justifyContent: 'flex-start' }}>
                  New License
                </Button>
              </NavLink>
            </Tooltip>
            <Tooltip title="Add Publisher">
              <NavLink to="/publishers" onClick={handleDrawerClose}>
                <Button startIcon={<BusinessIcon />} variant="outlined" size="small" fullWidth sx={{ justifyContent: 'flex-start' }}>
                  New Publisher
                </Button>
              </NavLink>
            </Tooltip>
          </Box>
        </Box>
      )}

      {!collapsed && (
        <Box sx={{ p: 2, mt: 'auto' }}>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            STATUS
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
            <Box sx={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: 'success.main' }} />
            <Typography variant="body2">API Connected</Typography>
          </Box>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Box sx={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: 'primary.main' }} />
            <Typography variant="body2">WebSocket Active</Typography>
          </Box>
        </Box>
      )}
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar
        position="fixed"
        sx={{
          width: { md: `calc(100% - ${drawerWidth}px)` },
          ml: { md: `${drawerWidth}px` },
          transition: theme.transitions.create(['width', 'margin'], {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.enteringScreen,
          }),
        }}
      >
        <Toolbar>
          <IconButton
            color="inherit"
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            edge="start"
            onClick={() => setCollapsed(!collapsed)}
            sx={{ mr: 2, display: { xs: 'block', md: 'none' } }}
          >
            <MenuIcon />
          </IconButton>
          <IconButton
            color="inherit"
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            edge="start"
            onClick={() => setCollapsed(!collapsed)}
            sx={{ mr: 2, display: { xs: 'none', md: 'block' } }}
          >
            {collapsed ? <ChevronRightIcon /> : <ChevronLeftIcon />}
          </IconButton>
          
          <Box sx={{ flexGrow: 1 }} />
          
          <Tooltip title="Refresh">
            <IconButton color="inherit" onClick={() => window.location.reload()}>
              <Refresh />
            </IconButton>
          </Tooltip>
          
          <Tooltip title="Notifications">
            <IconButton color="inherit">
              <Badge badgeContent={3} color="error">
                <Notifications />
              </Badge>
            </IconButton>
          </Tooltip>

          <Tooltip title="Theme">
            <IconButton color="inherit">
              <Brightness4 />
            </IconButton>
          </Tooltip>

          <Tooltip title="Help">
            <IconButton color="inherit">
              <Help />
            </IconButton>
          </Tooltip>

          <IconButton
            onClick={handleProfileMenuOpen}
            sx={{ ml: 1 }}
          >
            <Avatar
              sx={{ width: 36, height: 36 }}
              src={user?.avatar}
            >
              {user?.username?.charAt(0).toUpperCase() || 'U'}
            </Avatar>
          </IconButton>
        </Toolbar>
      </AppBar>

      <Menu
        anchorEl={anchorEl}
        open={Boolean(anchorEl)}
        onClose={handleProfileMenuClose}
        transformOrigin={{ horizontal: 'right', vertical: 'top' }}
        anchorOrigin={{ horizontal: 'right', vertical: 'bottom' }}
      >
        <Box sx={{ px: 1, py: 1 }}>
          <Typography variant="subtitle1" fontWeight={600}>{user?.username}</Typography>
          <Typography variant="body2" color="text.secondary">{user?.email}</Typography>
        </Box>
        <Divider />
        <MenuItem onClick={handleProfileMenuClose}>
          <ListItemIcon><Person /></ListItemIcon>
          <ListItemText>Profile</ListItemText>
        </MenuItem>
        <MenuItem onClick={handleProfileMenuClose}>
          <ListItemIcon><Settings /></ListItemIcon>
          <ListItemText>Settings</ListItemText>
        </MenuItem>
        <Divider />
        <MenuItem onClick={handleLogout}>
          <ListItemIcon><Logout /></ListItemIcon>
          <ListItemText>Logout</ListItemText>
        </MenuItem>
      </Menu>

      <Drawer
        variant={isMobile ? 'temporary' : 'permanent'}
        open={isMobile ? mobileOpen : true}
        onClose={handleDrawerClose}
        sx={{
          width: { md: collapsed ? collapsedDrawerWidth : drawerWidth },
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: { md: collapsed ? collapsedDrawerWidth : drawerWidth },
            boxSizing: 'border-box',
            borderRight: '1px solid',
            borderColor: 'divider',
            transition: theme.transitions.create('width', {
              easing: theme.transitions.easing.sharp,
              duration: theme.transitions.duration.enteringScreen,
            }),
            overflow: 'hidden',
          },
        }}
      >
        {drawerContent}
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          p: 3,
          width: { md: `calc(100% - ${drawerWidth}px)` },
          ml: { md: `${drawerWidth}px` },
          transition: theme.transitions.create(['margin', 'width'], {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.enteringScreen,
          }),
          minHeight: '100vh',
          backgroundColor: 'background.default',
        }}
      >
        <Toolbar />
        <Box sx={{ maxWidth: '1400px', mx: 'auto', width: '100%' }}>
          {React.Children.map(children, (child) =>
            React.isValidElement(child) ? React.cloneElement(child, { collapsed, collapsedDrawerWidth }) : child
          )}
        </Box>
      </Box>
    </Box>
  );
};

import { useAuth } from '../stores/authStore';
import { useLicenseStore } from '../stores/licenseStore';
import { socket } from '../services/socket';
import { useState } from 'react';
import { Button } from '@mui/material';

export default Layout;
