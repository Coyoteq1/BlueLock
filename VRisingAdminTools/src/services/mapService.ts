// Map Service - API client for player positions and map data

export interface Player {
  id: string;
  name: string;
  x: number;
  y: number;
  hp: number;
  maxHp: number;
  guild?: string;
  isOnline: boolean;
  lastSeen: string;
}

export interface PlayerUpdate {
  timestamp: string;
  moved: { id: string; x: number; y: number }[];
  joined: Player[];
  left: string[];
}

export interface MapZone {
  id: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  type: 'arena' | 'spawn' | 'trap' | 'glow';
  color?: string;
}

// V Rising world bounds - 4096x4096 units (standard V Rising map)
// Center is at (0, 0), ranges from -2048 to +2048 on both axes
export const WORLD_BOUNDS = {
  minX: -2048,
  maxX: 2048,
  minY: -2048,
  maxY: 2048,
  width: 4096,
  height: 4096,
};

// Map tile configuration (vrising.gaming.tools)
export const MAP_CONFIG = {
  // Tile URL pattern - check actual API for correct format
  tileUrl: 'https://vrising.gaming.tools/{z}/{x}/{y}.png',
  minZoom: 0,
  maxZoom: 5,
  tileSize: 256,
  // V Rising uses Web Mercator-like projection for tiles
  // At zoom 0: 1 tile covers entire world
  // At zoom n: 2^n tiles per axis
};

class MapService {
  private serverUrl: string = '';
  private apiKey: string = '';
  private pollingInterval: number | null = null;
  private lastUpdateTime: string = '';
  private players: Map<string, Player> = new Map();

  // Initialize with server configuration
  configure(serverUrl: string, apiKey: string = '') {
    this.serverUrl = serverUrl.replace(/\/$/, '');
    this.apiKey = apiKey;
  }

  // Fetch all players
  async fetchPlayers(): Promise<Player[]> {
    try {
      const response = await fetch(`${this.serverUrl}/api/players`, {
        headers: this.getHeaders(),
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const players = await response.json() as Player[];
      
      // Update local cache
      players.forEach(p => this.players.set(p.id, p));
      
      return players;
    } catch (error) {
      console.error('Failed to fetch players:', error);
      return Array.from(this.players.values());
    }
  }

  // Poll for player updates (real-time movement)
  async pollPlayerUpdates(): Promise<PlayerUpdate | null> {
    try {
      const url = new URL(`${this.serverUrl}/api/players/update`);
      if (this.lastUpdateTime) {
        url.searchParams.set('since', this.lastUpdateTime);
      }

      const response = await fetch(url.toString(), {
        headers: this.getHeaders(),
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const update = await response.json() as PlayerUpdate;
      this.lastUpdateTime = update.timestamp;

      // Apply updates
      update.moved.forEach(m => {
        const player = this.players.get(m.id);
        if (player) {
          player.x = m.x;
          player.y = m.y;
        }
      });

      update.joined.forEach(p => this.players.set(p.id, p));
      update.left.forEach(id => this.players.delete(id));

      return update;
    } catch (error) {
      console.error('Failed to poll player updates:', error);
      return null;
    }
  }

  // Start polling for updates
  startPolling(callback: (update: PlayerUpdate | null) => void, intervalMs: number = 2000) {
    this.stopPolling();
    this.pollingInterval = window.setInterval(() => {
      this.pollPlayerUpdates().then(callback);
    }, intervalMs);
  }

  // Stop polling
  stopPolling() {
    if (this.pollingInterval !== null) {
      clearInterval(this.pollingInterval);
      this.pollingInterval = null;
    }
  }

  // Get all cached players
  getPlayers(): Player[] {
    return Array.from(this.players.values());
  }

  // Get player by ID
  getPlayer(id: string): Player | undefined {
    return this.players.get(id);
  }

  // World coordinate to tile coordinate
  worldToTile(worldX: number, worldY: number, zoom: number): { x: number; y: number } {
    // V Rising map uses a specific projection
    // Adjust these values based on the actual vrising-map.com tile structure
    const scale = Math.pow(2, zoom);
    const tileX = Math.floor((worldX + WORLD_BOUNDS.maxX) * scale / (WORLD_BOUNDS.maxX * 2));
    const tileY = Math.floor((worldY + WORLD_BOUNDS.maxY) * scale / (WORLD_BOUNDS.maxY * 2));
    return { x: tileX, y: tileY };
  }

  // Tile coordinate to world coordinate
  tileToWorld(tileX: number, tileY: number, zoom: number): { x: number; y: number } {
    const scale = Math.pow(2, zoom);
    const worldX = tileX * (WORLD_BOUNDS.maxX * 2) / scale - WORLD_BOUNDS.maxX;
    const worldY = tileY * (WORLD_BOUNDS.maxY * 2) / scale - WORLD_BOUNDS.maxY;
    return { x: worldX, y: worldY };
  }

  // Get tile URL for a specific coordinate
  getTileUrl(x: number, y: number, zoom: number): string {
    return MAP_CONFIG.tileUrl
      .replace('{z}', zoom.toString())
      .replace('{x}', x.toString())
      .replace('{y}', y.toString());
  }

  // Get headers for API requests
  private getHeaders(): HeadersInit {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
    };
    if (this.apiKey) {
      headers['X-API-Key'] = this.apiKey;
    }
    return headers;
  }
}

// Export singleton instance
export const mapService = new MapService();

// Export class for custom instances
export { MapService };
