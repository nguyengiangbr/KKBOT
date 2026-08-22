import React, { useState } from 'react';
import { View, Text, KeyboardAvoidingView, Platform, ScrollView, ActivityIndicator, Alert } from 'react-native';
import { TextInput, Button, Card, Title, Paragraph, Link, Snackbar } from 'react-native-paper';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { styles, formStyles } from './styles';

export const LoginScreen = () => {
  const { colors } = useTheme();
  const { login, isLoading: authLoading } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [emailError, setEmailError] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [snackbarVisible, setSnackbarVisible] = useState(false);
  const [snackbarMessage, setSnackbarMessage] = useState('');

  const validateForm = () => {
    let valid = true;
    if (!email) {
      setEmailError('Email is required');
      valid = false;
    } else if (!/\S+@\S+\.\S+/.test(email)) {
      setEmailError('Invalid email format');
      valid = false;
    } else {
      setEmailError('');
    }

    if (!password) {
      setPasswordError('Password is required');
      valid = false;
    } else if (password.length < 8) {
      setPasswordError('Password must be at least 8 characters');
      valid = false;
    } else {
      setPasswordError('');
    }

    return valid;
  };

  const handleLogin = async () => {
    if (!validateForm()) return;

    setIsLoading(true);
    try {
      await login({ email, password });
    } catch (error: any) {
      const message = error.response?.data?.error || error.message || 'Login failed';
      showSnackbar(message);
    } finally {
      setIsLoading(false);
    }
  };

  const showSnackbar = (message: string) => {
    setSnackbarMessage(message);
    setSnackbarVisible(true);
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      style={[styles.container, { backgroundColor: colors.background }]}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 0 : 0}
    >
      <ScrollView
        contentContainerStyle={styles.scrollContent}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        <View style={styles.header}>
          <View style={styles.logoContainer}>
            <Ionicons name="smart-toy" size={80} color={colors.primary} />
          </View>
          <Title style={styles.title}>Welcome to KKBOT</Title>
          <Paragraph style={styles.subtitle}>Sign in to access your dashboard</Paragraph>
        </View>

        <Card style={styles.card} elevation={3}>
          <Card.Content style={styles.cardContent}>
            <TextInput
              label="Email"
              value={email}
              onChangeText={text => {
                setEmail(text);
                if (emailError) setEmailError('');
              }}
              mode="outlined"
              keyboardType="email-address"
              autoCapitalize="none"
              autoCompleteType="email"
              textContentType="email"
              error={!!emailError}
              helperText={emailError}
              style={styles.input}
              placeholder="you@example.com"
            />

            <TextInput
              label="Password"
              value={password}
              onChangeText={text => {
                setPassword(text);
                if (passwordError) setPasswordError('');
              }}
              mode="outlined"
              secureTextEntry
              textContentType="password"
              error={!!passwordError}
              helperText={passwordError}
              style={[styles.input, { marginTop: 16 }]}
              placeholder="••••••••"
            />

            <View style={styles.forgotPasswordContainer}>
              <Link onPress={() => Alert.alert('Forgot Password', 'Password reset not implemented yet')}>
                Forgot password?
              </Link>
            </View>

            <Button
              mode="contained"
              style={[styles.submitButton, { backgroundColor: colors.primary }]}
              onPress={handleLogin}
              loading={isLoading || authLoading}
              disabled={isLoading || authLoading}
              style={{ marginTop: 24, height: 50 }}
            >
              Sign In
            </Button>
          </Card.Content>
        </Card>

        <View style={styles.registerLink}>
          <Paragraph style={{ color: colors.onSurfaceVariant }}>
            Don&apos;t have an account?{' '}
            <Link onPress={() => Alert.alert('Register', 'Registration not implemented yet')}>
              Sign up
            </Link>
          </Paragraph>
        </View>

        <View style={styles.versionInfo}>
          <Text style={{ color: colors.onSurfaceVariant, fontSize: 12 }}>
            KKBOT Mobile v1.0.0
          </Text>
        </View>
      </ScrollView>

      <Snackbar
        visible={snackbarVisible}
        onDismiss={() => setSnackbarVisible(false)}
        action={{ label: 'OK', onPress: () => setSnackbarVisible(false) }}
        duration={5000}
      >
        {snackbarMessage}
      </Snackbar>
    </KeyboardAvoidingView>
  );
};

const showSnackbar = (message: string) => {
  // This would be handled by the component's state
  console.log('Snackbar:', message);
};

import { styles, formStyles } from './styles';