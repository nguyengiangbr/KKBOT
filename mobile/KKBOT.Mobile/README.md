# KKBOT Mobile

Mobile companion app for the KKBOT Game Automation Platform.

## Features

- **Real-time Dashboard** - Monitor instances, jobs, and system health
- **Instance Management** - Create, start, stop, and delete game instances
- **Job Tracking** - View job queue, status, and execution history
- **Plugin Management** - Browse installed plugins and marketplace
- **AI/ML Pipeline** - Manage models, training jobs, and experiments
- **Push Notifications** - Real-time alerts for job completion, errors, and system events
- **Offline Support** - Cached data with background sync
- **Dark/Light Theme** - Automatic or manual theme switching
- **Multi-tenant Support** - Switch between tenants (SuperAdmin)

## Tech Stack

- **Framework**: Expo SDK 50 (React Native 0.73)
- **Language**: TypeScript 5.3
- **Navigation**: React Navigation 6 (Stack + Bottom Tabs)
- **State Management**: React Context + TanStack Query (React Query v5)
- **UI Components**: React Native Paper (Material Design 3)
- **Icons**: Expo Vector Icons (Ionicons, MaterialCommunityIcons)
- **Storage**: AsyncStorage (encrypted with expo-secure-store)
- **Notifications**: Expo Notifications (push + local)
- **Networking**: Axios with interceptors (auto token refresh)
- **Testing**: Jest + React Native Testing Library
- **CI/CD**: EAS Build + GitHub Actions

## Project Structure

```
mobile/KKBOT.Mobile/
├── src/
│   ├── components/          # Reusable UI components
│   ├── context/             # React Context providers
│   │   ├── AuthContext.tsx
│   │   ├── ThemeContext.tsx
│   │   └── NotificationContext.tsx
│   ├── hooks/               # Custom React hooks
│   ├── navigation/          # Navigation configuration
│   │   └── AppNavigator.tsx
│   ├── screens/             # Screen components
│   │   ├── DashboardScreen.tsx
│   │   ├── InstancesScreen.tsx
│   │   ├── JobsScreen.tsx
│   │   ├── PluginsScreen.tsx
│   │   ├── MarketplaceScreen.tsx
│   │   ├── AIModelsScreen.tsx
│   │   ├── AIDatasetsScreen.tsx
│   │   ├── AIExperimentsScreen.tsx
│   │   ├── UsersScreen.tsx
│   │   ├── SettingsScreen.tsx
│   │   ├── LoginScreen.tsx
│   │   ├── NotificationsScreen.tsx
│   │   └── ProfileScreen.tsx
│   ├── services/            # API services
│   │   └── api.ts
│   ├── theme/               # Theme configuration
│   │   └── theme.ts
│   ├── types/               # TypeScript type definitions
│   │   └── auth.ts
│   └── utils/               # Utility functions
│       └── errorBoundary.tsx
├── assets/                  # Images, fonts, etc.
├── App.tsx                  # App entry point
├── app.json                 # Expo configuration
├── eas.json                 # EAS Build configuration
├── package.json
├── tsconfig.json
└── README.md
```

## Getting Started

### Prerequisites

- Node.js 18+
- Expo CLI (`npm install -g expo-cli`)
- EAS CLI (`npm install -g eas-cli`)
- iOS Simulator (Mac) / Android Emulator
- Physical device for push notifications testing

### Installation

```bash
cd mobile/KKBOT.Mobile
npm install
```

### Development

```bash
# Start development server
npm start

# Run on iOS simulator
npm run ios

# Run on Android emulator
npm run android

# Run on web
npm run web
```

### Environment Variables

Create a `.env` file in the root:

```env
EXPO_PUBLIC_API_URL=http://localhost:8080/api/v1
EXPO_PUBLIC_WS_URL=ws://localhost:8080
EXPO_PUBLIC_PROJECT_ID=your-expo-project-id
```

### Building for Production

```bash
# Configure EAS
eas login
eas build:configure

# Build for Android (AAB for Play Store)
npm run build:android

# Build for iOS (App Store)
npm run build:ios

# Build preview (internal distribution)
npm run build:preview
```

## API Integration

The app communicates with the KKBOT backend via REST API:

- **Base URL**: Configurable via `EXPO_PUBLIC_API_URL`
- **Authentication**: JWT Bearer tokens with automatic refresh
- **Endpoints**: Full CRUD for instances, jobs, plugins, AI models, datasets, experiments, pipelines, users, tenants

### Key API Features

- Automatic token refresh on 401 responses
- Request/response interceptors for logging
- Offline queue for failed requests
- Optimistic updates for mutations

## Push Notifications

### Setup

1. Configure Expo project ID in `app.json`
2. Set up Firebase (Android) / APNs (iOS) credentials
3. Configure `expo-notifications` plugin in `app.json`

### Notification Types

- Job completed/failed
- Instance started/stopped/error
- System health alerts
- Training job completion
- Model deployment status
- Security alerts

### Handling

- Foreground: In-app banner
- Background: System notification tray
- Tapped: Deep link to relevant screen

## Theming

- **Light/Dark/System** modes supported
- MD3 (Material Design 3) color system
- Custom color palette matching KKBOT brand
- Persisted preference in AsyncStorage

## Multi-tenancy

- Header-based tenant resolution (`X-Tenant-ID`)
- Subdomain-based resolution
- JWT claim-based resolution
- Tenant-scoped data isolation

## Testing

```bash
# Run unit tests
npm test

# Run with coverage
npm test -- --coverage

# Run in watch mode
npm run test:watch
```

## Code Quality

```bash
# Lint
npm run lint

# Type check
npm run typecheck

# Format
npm run format
```

## Deployment

### EAS Build

```bash
# Development build
eas build --profile development

# Preview build
eas build --profile preview

# Production build
eas build --profile production
```

### App Store / Play Store

```bash
# Submit to stores
eas submit --platform android
eas submit --platform ios
```

## Security

- **Secure Storage**: expo-secure-store for tokens
- **Network Security**: Certificate pinning ready
- **Biometric Auth**: Ready for FaceID/TouchID integration
- **Certificate Pinning**: Configurable via `expo-updates`

## Contributing

1. Fork the repository
2. Create a feature branch
2. Make your changes
3. Run tests and linting
4. Submit a Pull Request

## License

MIT License - see LICENSE file for details.

## Support

- Documentation: https://docs.kkbot.example.com/mobile
- Issues: https://github.com/your-org/kkbot/issues
- Discord: https://discord.gg/kkbot