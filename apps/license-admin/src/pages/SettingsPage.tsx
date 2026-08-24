import React from 'react';
import { Grid, Card, CardContent, CardHeader, Typography, Box, Button, TextField, FormControl, InputLabel, Select, MenuItem, Switch, FormControlLabel, FormGroup } from '@mui/material';
import { Save as SaveIcon, Security as SecurityIcon, Notifications as NotificationsIcon, Palette as PaletteIcon, Language as LanguageIcon } from '@mui/icons-material';
import { useAuth } from '../stores/authStore';

const SettingsPage = () => {
  const { user } = useAuth();
  const [generalSettings, setGeneralSettings] = React.useState({
    theme: 'light',
    language: 'en',
    autoRefresh: true,
    refreshInterval: 30000,
  });
  const [securitySettings, setSecuritySettings] = React.useState({
    twoFactorEnabled: false,
    sessionTimeout: 30,
    passwordExpiry: 90,
  });
  const [notificationSettings, setNotificationSettings] = React.useState({
    emailAlerts: true,
    pushNotifications: true,
    licenseExpiryWarnings: true,
    systemAlerts: true,
  });

  const handleSave = (section) => {
    // Save settings to localStorage or API
    console.log('Saving', section);
  };

  return (
    <Box sx={{ flexGrow: 1, p: 3 }}>
      <Typography variant="h4" fontWeight={700} sx={{ mb: 3 }}>Settings</Typography>

      <Grid container spacing={3}>
        {/* General Settings */}
        <Grid item xs={12} lg={6}>
          <Card>
            <CardHeader 
              title="General" 
              action={<PaletteIcon />}
            />
            <CardContent>
              <FormControl fullWidth sx={{ mb: 2 }}>
                <InputLabel id="theme-label">Theme</InputLabel>
                <Select value={generalSettings.theme} label="Theme" onChange={(e) => setGeneralSettings({...generalSettings, theme: e.target.value})}>
                  <MenuItem value="light">Light</MenuItem>
                  <MenuItem value="dark">Dark</MenuItem>
                  <MenuItem value="system">System</MenuItem>
                </Select>
              </FormControl>
              <FormControl fullWidth sx={{ mb: 2 }}>
                <InputLabel id="language-label">Language</InputLabel>
                <Select value={generalSettings.language} label="Language" onChange={(e) => setGeneralSettings({...generalSettings, language: e.target.value})}>
                  <MenuItem value="en">English</MenuItem>
                  <MenuItem value="vi">Vietnamese</MenuItem>
                </Select>
              </FormControl>
              <FormControl fullWidth sx={{ mb: 2 }}>
                <InputLabel id="refresh-label">Auto Refresh Interval</InputLabel>
                <Select value={generalSettings.refreshInterval} label="Refresh Interval (ms)" onChange={(e) => setGeneralSettings({...generalSettings, refreshInterval: parseInt(e.target.value)})}>
                  <MenuItem value={10000}>10 seconds</MenuItem>
                  <MenuItem value={30000}>30 seconds</MenuItem>
                  <MenuItem value={60000}>1 minute</MenuItem>
                  <MenuItem value={300000}>5 minutes</MenuItem>
                </Select>
              </FormControl>
              <FormControlLabel
                control={<Switch checked={generalSettings.autoRefresh} onChange={(e) => setGeneralSettings({...generalSettings, autoRefresh: e.target.checked})} />}
                label="Auto Refresh"
              />
            </CardContent>
          </Card>
        </Grid>

        {/* Security Settings */}
        <Grid item xs={12} lg={6}>
          <Card>
            <CardHeader 
              title="Security" 
              action={<SecurityIcon />}
            />
            <CardContent>
              <FormControlLabel
                control={<Switch checked={securitySettings.twoFactorEnabled} onChange={(e) => setSecuritySettings({...securitySettings, twoFactorEnabled: e.target.checked})} />}
                label="Two-Factor Authentication"
              />
              <FormControl fullWidth sx={{ mt: 2, mb: 2 }}>
                <InputLabel id="session-timeout-label">Session Timeout (minutes)</InputLabel>
                <Select value={securitySettings.sessionTimeout} label="Session Timeout" onChange={(e) => setSecuritySettings({...securitySettings, sessionTimeout: parseInt(e.target.value)})}>
                  <MenuItem value={15}>15 minutes</MenuItem>
                  <MenuItem value={30}>30 minutes</MenuItem>
                  <MenuItem value={60}>1 hour</MenuItem>
                  <MenuItem value={120}>2 hours</MenuItem>
                </Select>
              </FormControl>
              <FormControl fullWidth sx={{ mb: 2 }}>
                <InputLabel id="password-expiry-label">Password Expiry (days)</InputLabel>
                <Select value={securitySettings.passwordExpiry} label="Password Expiry" onChange={(e) => setSecuritySettings({...securitySettings, passwordExpiry: parseInt(e.target.value)})}>
                  <MenuItem value={30}>30 days</MenuItem>
                  <MenuItem value={60}>60 days</MenuItem>
                  <MenuItem value={90}>90 days</MenuItem>
                  <MenuItem value={180}>180 days</MenuItem>
                </Select>
              </FormControl>
            </CardContent>
          </Card>
        </Grid>

        {/* Notification Settings */}
        <Grid item xs={12}>
          <Card>
            <CardHeader 
              title="Notifications" 
              action={<NotificationsIcon />}
            />
            <CardContent>
              <FormControlLabel
                control={<Switch checked={notificationSettings.emailAlerts} onChange={(e) => setNotificationSettings({...notificationSettings, emailAlerts: e.target.checked})} />}
                label="Email Alerts"
              />
              <FormControlLabel
                control={<Switch checked={notificationSettings.pushNotifications} onChange={(e) => setNotificationSettings({...notificationSettings, pushNotifications: e.target.checked})} />}
                label="Push Notifications"
              />
              <FormControlLabel
                control={<Switch checked={notificationSettings.licenseExpiryWarnings} onChange={(e) => setNotificationSettings({...notificationSettings, licenseExpiryWarnings: e.target.checked})} />}
                label="License Expiry Warnings"
              />
              <FormControlLabel
                control={<Switch checked={notificationSettings.systemAlerts} onChange={(e) => setNotificationSettings({...notificationSettings, systemAlerts: e.target.checked})} />}
                label="System Alerts"
              />
            </CardContent>
          </Card>
        </Grid>

        {/* Account Info */}
        <Grid item xs={12}>
          <Card>
            <CardHeader title="Account Information" />
            <CardContent>
              <Grid container spacing={2}>
                <Grid item xs={12} sm={6}>
                  <TextField fullWidth label="Username" value={user?.username} disabled />
                </Grid>
                <Grid item xs={12} sm={6}>
                  <TextField fullWidth label="Email" value={user?.email} disabled />
                </Grid>
                <Grid item xs={12} sm={6}>
                  <TextField fullWidth label="Role" value={user?.roles?.join(', ') || 'User'} disabled />
                </Grid>
                <Grid item xs={12} sm={6}>
                  <TextField fullWidth label="Publisher" value={user?.publisherId || 'N/A'} disabled />
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        </Grid>

        {/* Save Buttons */}
        <Grid item xs={12}>
          <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 2 }}>
            <Button variant="outlined" onClick={() => handleSave('general')}>Save General</Button>
            <Button variant="outlined" onClick={() => handleSave('security')}>Save Security</Button>
            <Button variant="contained" onClick={() => handleSave('notifications')}>Save Notifications</Button>
          </Box>
        </Grid>
      </Grid>
    </Box>
  );
};

import React from 'react';
import { Grid, Card, CardContent, CardHeader, Typography, Box, Button, TextField, FormControl, InputLabel, Select, MenuItem, Switch, FormControlLabel, FormGroup, FormControlLabel } from '@mui/material';
import { Save as SaveIcon, Security as SecurityIcon, Notifications as NotificationsIcon, Palette as PaletteIcon, Language as LanguageIcon } from '@mui/icons-material';
import { useAuth } from '../stores/authStore';

export default SettingsPage;
