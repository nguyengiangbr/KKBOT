import React, { Component, ErrorInfo, ReactNode } from 'react';
import { View, Text, Button, Card } from 'react-native-paper';
import { useTheme } from '../context/ThemeContext';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    error: null,
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Uncaught error:', error, errorInfo);
    // In production, send to error reporting service (Sentry, Bugsnag, etc.)
  }

  public render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      const { colors } = useTheme();
      return (
        <View style={styles.container}>
          <Card style={styles.card} elevation={3}>
            <Card.Content style={styles.content}>
              <Text style={styles.title}>Something went wrong</Text>
              <Text style={styles.message}>
                {this.state.error?.message || 'An unexpected error occurred'}
              </Text>
              {__DEV__ && this.state.error && (
                <Text style={styles.stack}>{this.state.error.stack}</Text>
              )}
              <Button
                mode="contained"
                onPress={() => this.setState({ hasError: false, error: null })}
                style={styles.button}
              >
                Try Again
              </Button>
            </Card.Content>
          </Card>
        </View>
      );
    }

    return this.props.children;
  }
}

export const errorBoundary = (Component: React.ComponentType<any>) => {
  return function WithErrorBoundary(props: any) {
    return (
      <ErrorBoundary>
        <Component {...props} />
      </ErrorBoundary>
    );
  };
};

const styles = {
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  card: {
    margin: 16,
    width: '100%',
  },
  content: {
    padding: 24,
    alignItems: 'center',
  },
  title: {
    fontSize: 20,
    fontWeight: '600',
    marginBottom: 12,
  },
  message: {
    textAlign: 'center',
    marginBottom: 16,
  },
  stack: {
    fontSize: 12,
    fontFamily: 'monospace',
    marginBottom: 16,
    textAlign: 'center',
  },
  button: {
    marginTop: 16,
  },
};

import { useTheme } from '../context/ThemeContext';