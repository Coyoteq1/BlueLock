# V Rising Admin Tools

A desktop GUI application for managing V Rising server automation plugins.

## Features

- **Dashboard** - Server status overview with quick actions
- **Zone Management** - Control arena glow borders and zone settings
- **Trap System** - Manage container traps, trap zones, chests, and kill streaks
- **Configuration Editor** - Live editing of plugin configurations
- **Real-time Logs** - Filterable event log with WebSocket support

## Installation

### Prerequisites

- Node.js 18+
- npm or pnpm

### Setup

```bash
# Navigate to the project directory
cd VRisingAdminTools

# Install dependencies
npm install

# Start development server
npm run dev

# Build for production
npm run build
```

### Running the Application

```bash
# Development mode
npm run dev

# Production build
npm run preview
```

## Configuration

1. Start the V Rising server with VAuto plugins
2. Configure the HTTP API port in the plugin configuration
3. Open the Admin Tools application
4. Enter the server URL and API key in the header
5. Click "Connect" to start managing your server

## API Endpoints

The GUI connects to these endpoints on your V Rising server:

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/status` | Server status |
| GET | `/api/zones` | List zones |
| POST | `/api/zones/glow/spawn` | Spawn glow borders |
| POST | `/api/zones/glow/clear` | Clear glow borders |
| GET | `/api/traps` | List traps |
| POST | `/api/traps/set` | Set trap |
| POST | `/api/chests/spawn` | Spawn chest |
| GET | `/api/config` | Get configuration |
| PUT | `/api/config` | Update configuration |
| GET | `/api/logs` | Get event logs |

## Building for Distribution

```bash
# Windows
npm run build -- --win

# macOS
npm run build -- --mac

# Linux
npm run build -- --linux
```

## Technology Stack

- **Electron** - Desktop framework
- **React 18** - UI components
- **TypeScript** - Type safety
- **Goober** - CSS-in-JS styling
- **Axios** - HTTP client

## License

MIT
