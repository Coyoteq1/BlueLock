import React, { useState, useEffect, useCallback, useRef } from 'react';
import { InteractiveMap, MapMarker, MapZone } from '../components/Map/InteractiveMap';
import { vRisingMapService, VMapPlayer, DEFAULT_BOUNDS } from '../services/vrisingMapService';

interface MapPageProps {
  serverUrl?: string;  // Optional - uses vrising.gaming.tools if not provided
  apiKey?: string;
}

const MapPage: React.FC<MapPageProps> = ({ serverUrl, apiKey }) => {
  const [selectedPlayer, setSelectedPlayer] = useState<MapMarker | null>(null);
  const [selectedZone, setSelectedZone] = useState<MapZone | null>(null);
  const [players, setPlayers] = useState<VMapPlayer[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);
  const [mapConfig, setMapConfig] = useState(DEFAULT_BOUNDS);
  
  const pollingRef = useRef<number | null>(null);

  // Fetch map configuration
  useEffect(() => {
    const loadConfig = async () => {
      const config = await vRisingMapService.getConfig();
      setMapConfig(config.bounds);
    };
    loadConfig();
  }, []);

  // Fetch players from vrising.gaming.tools
  const fetchPlayers = useCallback(async () => {
    try {
      const data = await vRisingMapService.getPlayers();
      setPlayers(data);
      setIsConnected(true);
      setError(null);
      setLastUpdate(new Date());
    } catch (err) {
      setError('Failed to fetch players from vrising.gaming.tools');
      setIsConnected(false);
    }
  }, []);

  // Initial fetch and polling
  useEffect(() => {
    fetchPlayers();

    // Poll every 5 seconds
    pollingRef.current = window.setInterval(() => {
      fetchPlayers();
    }, 5000);

    return () => {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
      }
    };
  }, [fetchPlayers]);

  // Convert VMapPlayer to MapMarker
  const playerMarkers: MapMarker[] = players.map(player => ({
    id: player.id,
    x: player.x,
    y: player.y,
    type: 'player' as const,
    name: player.name,
    data: { 
      hp: player.hp, 
      maxHp: player.maxHp,
      guild: player.guild,
      zone: player.zone,
    },
  }));

  // Zone definitions
  const zones: MapZone[] = [
    { id: 'arena-main', x: 0, y: 0, width: 300, height: 300, type: 'arena', name: 'Main Arena', color: '#f59e0b' },
    { id: 'spawn-north', x: -300, y: -200, width: 100, height: 100, type: 'spawn', name: 'North Spawn', color: '#22c55e' },
    { id: 'spawn-south', x: 200, y: 300, width: 100, height: 100, type: 'spawn', name: 'South Spawn', color: '#22c55e' },
    { id: 'spawn-east', x: 400, y: -100, width: 100, height: 100, type: 'spawn', name: 'East Spawn', color: '#22c55e' },
    { id: 'spawn-west', x: -400, y: 100, width: 100, height: 100, type: 'spawn', name: 'West Spawn', color: '#22c55e' },
    { id: 'trap-1', x: 150, y: 150, width: 50, height: 50, type: 'trap', name: 'Trap Zone 1', color: '#ef4444' },
    { id: 'glow-1', x: -100, y: 100, width: 80, height: 80, type: 'glow', name: 'Glow Zone', color: '#8b5cf6' },
    { id: 'glow-2', x: 100, y: -150, width: 80, height: 80, type: 'glow', name: 'Glow Zone 2', color: '#8b5cf6' },
  ];

  const handleMarkerClick = useCallback((marker: MapMarker) => {
    setSelectedPlayer(marker);
    setSelectedZone(null);
  }, []);

  const handleZoneClick = useCallback((zone: MapZone) => {
    setSelectedZone(zone);
    setSelectedPlayer(null);
  }, []);

  return (
    <div style={{ display: 'flex', gap: '16px', height: '100%' }}>
      {/* Map Section */}
      <div style={{ flex: 1 }}>
        <div style={{ marginBottom: '8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <div style={{
              width: '8px',
              height: '8px',
              borderRadius: '50%',
              background: isConnected ? '#4ade80' : '#ef4444',
            }} />
            <span style={{ color: '#8c91a0', fontSize: '12px' }}>
              {isConnected ? `${players.length} players on map` : 'Connecting...'}
            </span>
            {lastUpdate && (
              <span style={{ color: '#666', fontSize: '10px' }}>
                Last update: {lastUpdate.toLocaleTimeString()}
              </span>
            )}
          </div>
          <div style={{ display: 'flex', gap: '8px' }}>
            <span style={{ color: '#8b5cf6', fontSize: '11px' }}>
              vrising.gaming.tools
            </span>
            {error && (
              <span style={{ color: '#ef4444', fontSize: '11px' }}>{error}</span>
            )}
          </div>
        </div>
        <InteractiveMap
          markers={playerMarkers}
          zones={zones}
          width={700}
          height={500}
          onMarkerClick={handleMarkerClick}
          onZoneClick={handleZoneClick}
        />
      </div>

      {/* Details Panel */}
      <div style={{
        width: '280px',
        background: 'rgba(0,0,0,0.2)',
        border: '1px solid #383c4a',
        borderRadius: '8px',
        padding: '16px',
        overflow: 'auto',
      }}>
        <h3 style={{ color: '#ffffff', marginBottom: '16px', borderBottom: '1px solid #383c4a', paddingBottom: '8px' }}>
          Details
        </h3>

        {/* Selected Player */}
        {selectedPlayer && (
          <div>
            <div style={{ color: '#4ade80', fontSize: '14px', fontWeight: 'bold', marginBottom: '8px' }}>
              Player Details
            </div>
            <div style={{ color: '#8c91a0', fontSize: '12px' }}>
              <div style={{ marginBottom: '4px' }}>
                <span style={{ color: '#8c91a0' }}>Name:</span>{' '}
                <span style={{ color: '#ffffff' }}>{selectedPlayer.name}</span>
              </div>
              <div style={{ marginBottom: '4px' }}>
                <span style={{ color: '#8c91a0' }}>Position:</span>{' '}
                <span style={{ color: '#ffffff' }}>
                  ({selectedPlayer.x.toFixed(1)}, {selectedPlayer.y.toFixed(1)})
                </span>
              </div>
              {selectedPlayer.data && (
                <>
                  <div style={{ marginTop: '12px' }}>
                    <div style={{ color: '#8c91a0', marginBottom: '4px' }}>HP:</div>
                    <div style={{
                      background: '#383c4a',
                      borderRadius: '4px',
                      height: '8px',
                      overflow: 'hidden',
                    }}>
                      <div style={{
                        width: `${((selectedPlayer.data.hp as number) / (selectedPlayer.data.maxHp as number)) * 100}%`,
                        background: '#4ade80',
                        height: '100%',
                      }} />
                    </div>
                    <div style={{ color: '#8c91a0', fontSize: '10px', marginTop: '2px' }}>
                      {selectedPlayer.data.hp} / {selectedPlayer.data.maxHp}
                    </div>
                  </div>
                  {selectedPlayer.data.guild && (
                    <div style={{ marginTop: '8px' }}>
                      <span style={{ color: '#8c91a0' }}>Guild:</span>{' '}
                      <span style={{ color: '#f59e0b' }}>{selectedPlayer.data.guild}</span>
                    </div>
                  )}
                  {selectedPlayer.data.zone && (
                    <div style={{ marginTop: '4px' }}>
                      <span style={{ color: '#8c91a0' }}>Zone:</span>{' '}
                      <span style={{ color: '#8b5cf6' }}>{selectedPlayer.data.zone}</span>
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        )}

        {/* Selected Zone */}
        {selectedZone && (
          <div>
            <div style={{ color: COLORS[selectedZone.type], fontSize: '14px', fontWeight: 'bold', marginBottom: '8px' }}>
              Zone Details
            </div>
            <div style={{ color: '#8c91a0', fontSize: '12px' }}>
              <div style={{ marginBottom: '4px' }}>
                <span style={{ color: '#8c91a0' }}>Name:</span>{' '}
                <span style={{ color: '#ffffff' }}>{selectedZone.name}</span>
              </div>
              <div style={{ marginBottom: '4px' }}>
                <span style={{ color: '#8c91a0' }}>Type:</span>{' '}
                <span style={{ color: '#ffffff', textTransform: 'capitalize' }}>{selectedZone.type}</span>
              </div>
              <div style={{ marginBottom: '4px' }}>
                <span style={{ color: '#8c91a0' }}>Center:</span>{' '}
                <span style={{ color: '#ffffff' }}>
                  ({selectedZone.x.toFixed(0)}, {selectedZone.y.toFixed(0)})
                </span>
              </div>
              <div style={{ marginBottom: '4px' }}>
                <span style={{ color: '#8c91a0' }}>Size:</span>{' '}
                <span style={{ color: '#ffffff' }}>
                  {selectedZone.width}x{selectedZone.height}
                </span>
              </div>
            </div>
          </div>
        )}

        {/* No Selection */}
        {!selectedPlayer && !selectedZone && (
          <div style={{ color: '#8c91a0', fontSize: '12px', textAlign: 'center', padding: '20px 0' }}>
            Click on a player or zone to see details
          </div>
        )}

        {/* World Info */}
        <div style={{ marginTop: '16px', padding: '12px', background: 'rgba(255,255,255,0.05)', borderRadius: '4px' }}>
          <div style={{ color: '#ffffff', fontSize: '12px', fontWeight: 'bold', marginBottom: '8px' }}>
            World Info
          </div>
          <div style={{ color: '#8c91a0', fontSize: '11px' }}>
            <div>Bounds: {mapConfig.width}x{mapConfig.height}</div>
            <div>X: {mapConfig.minX} to {mapConfig.maxX}</div>
            <div>Y: {mapConfig.minY} to {mapConfig.maxY}</div>
            <div style={{ marginTop: '8px', color: '#8b5cf6' }}>
              Source: vrising.gaming.tools
            </div>
          </div>
        </div>

        {/* Player List */}
        <div style={{ marginTop: '24px' }}>
          <div style={{ 
            color: '#ffffff', 
            fontSize: '14px', 
            fontWeight: 'bold', 
            marginBottom: '8px',
            borderBottom: '1px solid #383c4a',
            paddingBottom: '8px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
          }}>
            <span>Players ({players.length})</span>
            <button
              onClick={fetchPlayers}
              style={{
                background: '#383c4a',
                border: '1px solid rgba(0,0,0,0.5)',
                borderRadius: '4px',
                color: '#ffffff',
                cursor: 'pointer',
                fontSize: '10px',
                padding: '4px 8px',
              }}
            >
              Refresh
            </button>
          </div>
          {players.length === 0 ? (
            <div style={{ color: '#666', fontSize: '11px', textAlign: 'center', padding: '10px' }}>
              No players on map
            </div>
          ) : (
            players.map(player => (
              <div
                key={player.id}
                onClick={() => handleMarkerClick({
                  id: player.id,
                  x: player.x,
                  y: player.y,
                  type: 'player',
                  name: player.name,
                  data: { hp: player.hp, maxHp: player.maxHp, guild: player.guild, zone: player.zone },
                })}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '8px',
                  background: 'rgba(255,255,255,0.05)',
                  borderRadius: '4px',
                  marginBottom: '4px',
                  cursor: 'pointer',
                }}
              >
                <div style={{
                  width: '8px',
                  height: '8px',
                  borderRadius: '50%',
                  background: player.guild ? '#f59e0b' : '#4ade80',
                }} />
                <span style={{ color: '#ffffff', fontSize: '12px' }}>{player.name}</span>
                {player.guild && (
                  <span style={{ color: '#8b5cf6', fontSize: '10px' }}>[{player.guild}]</span>
                )}
                <span style={{ color: '#8c91a0', fontSize: '10px', marginLeft: 'auto' }}>
                  ({player.x.toFixed(0)}, {player.y.toFixed(0)})
                </span>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
};

// Color helper
const COLORS: Record<string, string> = {
  player: '#4ade80',
  zone: '#3b82f6',
  trap: '#ef4444',
  spawn: '#22c55e',
  arena: '#f59e0b',
  glow: '#8b5cf6',
};

export default MapPage;
