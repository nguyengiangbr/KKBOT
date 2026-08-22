import { MD3LightTheme, MD3DarkTheme, configureFonts } from 'react-native-paper';

const fontConfig = {
  regular: {
    fontFamily: 'System',
    fontWeight: '400' as const,
  },
  medium: {
    fontFamily: 'System',
    fontWeight: '500' as const,
  },
  light: {
    fontFamily: 'System',
    fontWeight: '300' as const,
  },
  thin: {
    fontFamily: 'System',
    fontWeight: '100' as const,
  },
};

export const lightTheme = {
  ...MD3LightTheme,
  colors: {
    ...MD3LightTheme.colors,
    primary: '#1976D2',
    primaryContainer: '#E3F2FD',
    secondary: '#00695C',
    secondaryContainer: '#E0F2F1',
    tertiary: '#6A1B9A',
    tertiaryContainer: '#F3E5F5',
    error: '#D32F2F',
    errorContainer: '#FFEBEE',
    background: '#FAFAFA',
    surface: '#FFFFFF',
    surfaceVariant: '#F5F5F5',
    outline: '#757575',
    outlineVariant: '#BDBDBD',
    onPrimary: '#FFFFFF',
    onPrimaryContainer: '#0D47A1',
    onSecondary: '#FFFFFF',
    onSecondaryContainer: '#003D39',
    onSurface: '#212121',
    onSurfaceVariant: '#424242',
    onBackground: '#212121',
    onError: '#FFFFFF',
    onErrorContainer: '#B71C1C',
    inverseSurface: '#303030',
    inverseOnSurface: '#F5F5F5',
    inversePrimary: '#90CAF9',
    shadow: '#000000',
    scrim: '#000000',
    backdrop: 'rgba(0,0,0,0.5)',
  },
  fonts: configureFonts({ config: fontConfig }),
  roundness: 12,
  animation: {
    scale: 1.0,
  },
};

export const darkTheme = {
  ...MD3DarkTheme,
  colors: {
    ...MD3DarkTheme.colors,
    primary: '#90CAF9',
    primaryContainer: '#0D47A1',
    secondary: '#80CBC4',
    secondaryContainer: '#003D39',
    tertiary: '#CE93D8',
    tertiaryContainer: '#4A148C',
    error: '#EF9A9A',
    errorContainer: '#B71C1C',
    background: '#121212',
    surface: '#1E1E1E',
    surfaceVariant: '#2C2C2C',
    outline: '#9E9E9E',
    outlineVariant: '#757575',
    onPrimary: '#000000',
    onPrimaryContainer: '#E3F2FD',
    onSecondary: '#000000',
    onSecondaryContainer: '#E0F2F1',
    onSurface: '#FFFFFF',
    onSurfaceVariant: '#E0E0E0',
    onBackground: '#FFFFFF',
    onError: '#000000',
    onErrorContainer: '#FFEBEE',
    inverseSurface: '#F5F5F5',
    inverseOnSurface: '#212121',
    inversePrimary: '#1976D2',
    shadow: '#000000',
    scrim: '#000000',
    backdrop: 'rgba(0,0,0,0.7)',
  },
  fonts: configureFonts({ config: fontConfig }),
  roundness: 12,
  animation: {
    scale: 1.0,
  },
};

export const theme = {
  light: lightTheme,
  dark: darkTheme,
};

export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
};

export const borderRadius = {
  sm: 4,
  md: 8,
  lg: 12,
  xl: 16,
  full: 9999,
};

export const typography = {
  displayLarge: { fontSize: 57, fontWeight: '400', lineHeight: 64 },
  displayMedium: { fontSize: 45, fontWeight: '400', lineHeight: 52 },
  displaySmall: { fontSize: 36, fontWeight: '400', lineHeight: 44 },
  headlineLarge: { fontSize: 32, fontWeight: '400', lineHeight: 40 },
  headlineMedium: { fontSize: 28, fontWeight: '400', lineHeight: 36 },
  headlineSmall: { fontSize: 24, fontWeight: '400', lineHeight: 32 },
  titleLarge: { fontSize: 22, fontWeight: '500', lineHeight: 28 },
  titleMedium: { fontSize: 16, fontWeight: '500', lineHeight: 24 },
  titleSmall: { fontSize: 14, fontWeight: '500', lineHeight: 20 },
  bodyLarge: { fontSize: 16, fontWeight: '400', lineHeight: 24 },
  bodyMedium: { fontSize: 14, fontWeight: '400', lineHeight: 20 },
  bodySmall: { fontSize: 12, fontWeight: '400', lineHeight: 16 },
  labelLarge: { fontSize: 14, fontWeight: '500', lineHeight: 20 },
  labelMedium: { fontSize: 12, fontWeight: '500', lineHeight: 16 },
  labelSmall: { fontSize: 11, fontWeight: '500', lineHeight: 16 },
};