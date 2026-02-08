// VRising Map HTTP Service - Fetches map data from vrising.gaming.tools

export interface VMapTile {
  x: number;
  y: number;
  z: number;
  url: string;
}

export interface VMapPlayer {
  id: string;
  name: string;
  x: number;
  y: number;
  zone: string;
  hp: number;
  maxHp: number;
  guild?: string;
}

export interface VMapConfig {
  bounds: {
    minX: number;
    maxX: number;
    minY: number;
    maxY: number;
  };
  tileSize: number;
  minZoom: number;
  maxZoom: number;
}

// Default V Rising world bounds (4096x4096)
export const DEFAULT_BOUNDS = {
  minX: -2048,
  maxX: 2048,
  minY: -2048,
  maxY: 2048,
  width: 4096,
  height: 4096,
};

class VRisingMapService {
  private baseUrl = 'https://vrising.gaming.tools';
  private cache: Map<string, string> = new Map();
  private cacheTimeout = 5 * 60 * 1000; // 5 minutes

  // Fetch map configuration
  async getConfig(): Promise<VMapConfig> {
    try {
      const response = await fetch(`${this.baseUrl}/api/config`);
      if (!response.ok) throw new Error('Failed to fetch config');
      
      const data = await response.json();
      return {
        bounds: data.bounds || DEFAULT_BOUNDS,
        tileSize: data.tileSize || 256,
        minZoom: data.minZoom || 0,
        maxZoom: data.maxZoom || 5,
      };
    } catch (error) {
      console.error('Failed to fetch map config:', error);
      return {
        bounds: DEFAULT_BOUNDS,
        tileSize: 256,
        minZoom: 0,
        maxZoom: 5,
      };
    }
  }

  // Fetch all players on the map
  async getPlayers(): Promise<VMapPlayer[]> {
    try {
      const response = await fetch(`${this.baseUrl}/api/players`);
      if (!response.ok) throw new Error('Failed to fetch players');
      
      return await response.json();
    } catch (error) {
      console.error('Failed to fetch players:', error);
      return [];
    }
  }

  // Get tile URL for coordinates
  getTileUrl(x: number, y: number, zoom: number): string {
    return `${this.baseUrl}/tiles/${zoom}/${x}/${y}.png`;
  }

  // Preload tiles for a view area
  async preloadTiles(
    minX: number,
    maxX: number,
    minY: number,
    maxY: number,
    zoom: number
  ): Promise<string[]> {
    const tiles: string[] = [];
    
    // Calculate tile ranges
    const tilesPerSide = Math.pow(2, zoom);
    const tileWidth = (maxX - minX) / tilesPerSide;
    const tileHeight = (maxY - minY) / tilesPerSide;
    
    const startTileX = Math.floor((minX - DEFAULT_BOUNDS.minX) / (DEFAULT_BOUNDS.width / tilesPerSide));
    const endTileX = Math.floor((maxX - DEFAULT_BOUNDS.minX) / (DEFAULT_BOUNDS.width / tilesPerSide));
    const startTileY = Math.floor((minY - DEFAULT_BOUNDS.minY) / (DEFAULT_BOUNDS.height / tilesPerSide));
    const endTileY = Math.floor((maxY - DEFAULT_BOUNDS.minY) / (DEFAULT_BOUNDS.height / tilesPerSide));

    // Fetch tile URLs
    for (let x = startTileX; x <= endTileX; x++) {
      for (let y = startTileY; y <= endTileY; y++) {
        const url = this.getTileUrl(x, y, zoom);
        tiles.push(url);
        
        // Cache the tile URL
        this.cache.set(url, new Date().toISOString());
      }
    }

    return tiles;
  }

  // Get map statistics
  async getStats(): Promise<{
    playerCount: number;
    zones: string[];
    lastUpdate: string;
  }> {
    try {
      const response = await fetch(`${this.baseUrl}/api/stats`);
      if (!response.ok) throw new Error('Failed to fetch stats');
      
      return await response.json();
    } catch (error) {
      console.error('Failed to fetch stats:', error);
      return {
        playerCount: 0,
        zones: [],
        lastUpdate: new Date().toISOString(),
      };
    }
  }

  // Convert world coordinates to tile coordinates
  worldToTile(worldX: number, worldY: number, zoom: number): { x: number; y: number } {
    const tilesPerSide = Math.pow(2, zoom);
    
    // Normalize to 0-1 range
    const normalizedX = (worldX - DEFAULT_BOUNDS.minX) / DEFAULT_BOUNDS.width;
    const normalizedY = (worldY - DEFAULT_BOUNDS.minY) / DEFAULT_BOUNDS.height;
    
    return {
      x: Math.floor(normalizedX * tilesPerSide),
      y: Math.floor(normalizedY * tilesPerSide),
    };
  }

  // Convert tile coordinates to world coordinates
  tileToWorld(tileX: number, tileY: number, zoom: number): { x: number; y: number } {
    const tilesPerSide = Math.pow(2, zoom);
    
    return {
      x: DEFAULT_BOUNDS.minX + (tileX / tilesPerSide) * DEFAULT_BOUNDS.width,
      y: DEFAULT_BOUNDS.minY + (tileY / tilesPerSide) * DEFAULT_BOUNDS.height,
    };
  }

  // Clear cache
  clearCache(): void {
    this.cache.clear();
  }

  // Get cache info
  getCacheSize(): number {
    return this.cache.size;
  }
}

// Export singleton instance
export const vRisingMapService = new VRisingMapService();

// Export class for custom instances
export { VRisingMapService };
