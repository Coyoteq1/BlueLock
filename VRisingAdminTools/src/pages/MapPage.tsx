import React, { useState, useEffect, useCallback, useRef } from 'react';
import { InteractiveMap, MapMarker, MapZone } from '../components/Map/InteractiveMap';
import { vRisingMapService, VMapPlayer, DEFAULT_BOUNDS } from '../services/vrisingMapService';
interface MapPageProps {
  serverUrl?: string; // Optional - uses vrising.gaming.tools if not provided
  apiKey?: string;
}
const MapPage: React.FC<MapPageProps> = ({
  serverUrl,
  apiKey
}) => {
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
      zone: player.zone
    }
  }));

  // Zone definitions
  const zones: MapZone[] = [{
    id: 'arena-main',
    x: 0,
    y: 0,
    width: 300,
    height: 300,
    type: 'arena',
    name: 'Main Arena',
    color: '#f59e0b'
  }, {
    id: 'spawn-north',
    x: -300,
    y: -200,
    width: 100,
    height: 100,
    type: 'spawn',
    name: 'North Spawn',
    color: '#22c55e'
  }, {
    id: 'spawn-south',
    x: 200,
    y: 300,
    width: 100,
    height: 100,
    type: 'spawn',
    name: 'South Spawn',
    color: '#22c55e'
  }, {
    id: 'spawn-east',
    x: 400,
    y: -100,
    width: 100,
    height: 100,
    type: 'spawn',
    name: 'East Spawn',
    color: '#22c55e'
  }, {
    id: 'spawn-west',
    x: -400,
    y: 100,
    width: 100,
    height: 100,
    type: 'spawn',
    name: 'West Spawn',
    color: '#22c55e'
  }, {
    id: 'trap-1',
    x: 150,
    y: 150,
    width: 50,
    height: 50,
    type: 'trap',
    name: 'Trap Zone 1',
    color: '#ef4444'
  }, {
    id: 'glow-1',
    x: -100,
    y: 100,
    width: 80,
    height: 80,
    type: 'glow',
    name: 'Glow Zone',
    color: '#8b5cf6'
  }, {
    id: 'glow-2',
    x: 100,
    y: -150,
    width: 80,
    height: 80,
    type: 'glow',
    name: 'Glow Zone 2',
    color: '#8b5cf6'
  }];
  const handleMarkerClick = useCallback((marker: MapMarker) => {
    setSelectedPlayer(marker);
    setSelectedZone(null);
  }, []);
  const handleZoneClick = useCallback((zone: MapZone) => {
    setSelectedZone(zone);
    setSelectedPlayer(null);
  }, []);
  return <div style={{
    display: 'flex',
    gap: '16px',
    height: '100%'
  }} data-test="auto-MapPage-div-001">
      {/* Map Section */}
      <div style={{
      flex: 1
    }} data-test="auto-MapPage-div-002">
        <div style={{
        marginBottom: '8px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center'
      }} data-test="auto-MapPage-div-003">
          <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: '8px'
        }} data-test="auto-MapPage-div-004">
            <div style={{
            width: '8px',
            height: '8px',
            borderRadius: '50%',
            background: isConnected ? '#4ade80' : '#ef4444'
          }} data-test="auto-MapPage-div-005" />
            <span style={{
            color: '#8c91a0',
            fontSize: '12px'
          }} data-test="auto-MapPage-span-006">
              {isConnected ? `${players.length} players on map` : 'Connecting...'}
            </span>
            {lastUpdate && <span style={{
            color: '#666',
            fontSize: '10px'
          }} data-test="auto-MapPage-span-007">
                Last update: {lastUpdate.toLocaleTimeString()}
              </span>}
          </div>
          <div style={{
          display: 'flex',
          gap: '8px'
        }} data-test="auto-MapPage-div-008">
            <span style={{
            color: '#8b5cf6',
            fontSize: '11px'
          }} data-test="auto-MapPage-span-009">
              vrising.gaming.tools
            </span>
            {error && <span style={{
            color: '#ef4444',
            fontSize: '11px'
          }} data-test="auto-MapPage-span-010">{error}</span>}
          </div>
        </div>
        <InteractiveMap markers={playerMarkers} zones={zones} width={700} height={500} onMarkerClick={handleMarkerClick} onZoneClick={handleZoneClick} />
      </div>

      {/* Details Panel */}
      <div style={{
      width: '280px',
      background: 'rgba(0,0,0,0.2)',
      border: '1px solid #383c4a',
      borderRadius: '8px',
      padding: '16px',
      overflow: 'auto'
    }} data-test="auto-MapPage-div-011">
        <h3 style={{
        color: '#ffffff',
        marginBottom: '16px',
        borderBottom: '1px solid #383c4a',
        paddingBottom: '8px'
      }} data-test="auto-MapPage-h3-012">
          Details
        </h3>

        {/* Selected Player */}
        {selectedPlayer && <div data-test="auto-MapPage-div-013">
            <div style={{
          color: '#4ade80',
          fontSize: '14px',
          fontWeight: 'bold',
          marginBottom: '8px'
        }} data-test="auto-MapPage-div-014">
              Player Details
            </div>
            <div style={{
          color: '#8c91a0',
          fontSize: '12px'
        }} data-test="auto-MapPage-div-015">
              <div style={{
            marginBottom: '4px'
          }} data-test="auto-MapPage-div-016">
                <span style={{
              color: '#8c91a0'
            }} data-test="auto-MapPage-span-017">Name:</span>{' '}
                <span style={{
              color: '#ffffff'
            }} data-test="auto-MapPage-span-018">{selectedPlayer.name}</span>
              </div>
              <div style={{
            marginBottom: '4px'
          }} data-test="auto-MapPage-div-019">
                <span style={{
              color: '#8c91a0'
            }} data-test="auto-MapPage-span-020">Position:</span>{' '}
                <span style={{
              color: '#ffffff'
            }} data-test="auto-MapPage-span-021">
                  ({selectedPlayer.x.toFixed(1)}, {selectedPlayer.y.toFixed(1)})
                </span>
              </div>
              {selectedPlayer.data && <>
                  <div style={{
              marginTop: '12px'
            }} data-test="auto-MapPage-div-022">
                    <div style={{
                color: '#8c91a0',
                marginBottom: '4px'
              }} data-test="auto-MapPage-div-023">HP:</div>
                    <div style={{
                background: '#383c4a',
                borderRadius: '4px',
                height: '8px',
                overflow: 'hidden'
              }} data-test="auto-MapPage-div-024">
                      <div style={{
                  width: `${(selectedPlayer.data.hp as number) / (selectedPlayer.data.maxHp as number) * 100}%`,
                  background: '#4ade80',
                  height: '100%'
                }} data-test="auto-MapPage-div-025" />
                    </div>
                    <div style={{
                color: '#8c91a0',
                fontSize: '10px',
                marginTop: '2px'
              }} data-test="auto-MapPage-div-026">
                      {selectedPlayer.data.hp} / {selectedPlayer.data.maxHp}
                    </div>
                  </div>
                  {selectedPlayer.data.guild && <div style={{
              marginTop: '8px'
            }} data-test="auto-MapPage-div-027">
                      <span style={{
                color: '#8c91a0'
              }} data-test="auto-MapPage-span-028">Guild:</span>{' '}
                      <span style={{
                color: '#f59e0b'
              }} data-test="auto-MapPage-span-029">{selectedPlayer.data.guild}</span>
                    </div>}
                  {selectedPlayer.data.zone && <div style={{
              marginTop: '4px'
            }} data-test="auto-MapPage-div-030">
                      <span style={{
                color: '#8c91a0'
              }} data-test="auto-MapPage-span-031">Zone:</span>{' '}
                      <span style={{
                color: '#8b5cf6'
              }} data-test="auto-MapPage-span-032">{selectedPlayer.data.zone}</span>
                    </div>}
                </>}
            </div>
          </div>}

        {/* Selected Zone */}
        {selectedZone && <div data-test="auto-MapPage-div-033">
            <div style={{
          color: COLORS[selectedZone.type],
          fontSize: '14px',
          fontWeight: 'bold',
          marginBottom: '8px'
        }} data-test="auto-MapPage-div-034">
              Zone Details
            </div>
            <div style={{
          color: '#8c91a0',
          fontSize: '12px'
        }} data-test="auto-MapPage-div-035">
              <div style={{
            marginBottom: '4px'
          }} data-test="auto-MapPage-div-036">
                <span style={{
              color: '#8c91a0'
            }} data-test="auto-MapPage-span-037">Name:</span>{' '}
                <span style={{
              color: '#ffffff'
            }} data-test="auto-MapPage-span-038">{selectedZone.name}</span>
              </div>
              <div style={{
            marginBottom: '4px'
          }} data-test="auto-MapPage-div-039">
                <span style={{
              color: '#8c91a0'
            }} data-test="auto-MapPage-span-040">Type:</span>{' '}
                <span style={{
              color: '#ffffff',
              textTransform: 'capitalize'
            }} data-test="auto-MapPage-span-041">{selectedZone.type}</span>
              </div>
              <div style={{
            marginBottom: '4px'
          }} data-test="auto-MapPage-div-042">
                <span style={{
              color: '#8c91a0'
            }} data-test="auto-MapPage-span-043">Center:</span>{' '}
                <span style={{
              color: '#ffffff'
            }} data-test="auto-MapPage-span-044">
                  ({selectedZone.x.toFixed(0)}, {selectedZone.y.toFixed(0)})
                </span>
              </div>
              <div style={{
            marginBottom: '4px'
          }} data-test="auto-MapPage-div-045">
                <span style={{
              color: '#8c91a0'
            }} data-test="auto-MapPage-span-046">Size:</span>{' '}
                <span style={{
              color: '#ffffff'
            }} data-test="auto-MapPage-span-047">
                  {selectedZone.width}x{selectedZone.height}
                </span>
              </div>
            </div>
          </div>}

        {/* No Selection */}
        {!selectedPlayer && !selectedZone && <div style={{
        color: '#8c91a0',
        fontSize: '12px',
        textAlign: 'center',
        padding: '20px 0'
      }} data-test="auto-MapPage-div-048">
            Click on a player or zone to see details
          </div>}

        {/* World Info */}
        <div style={{
        marginTop: '16px',
        padding: '12px',
        background: 'rgba(255,255,255,0.05)',
        borderRadius: '4px'
      }} data-test="auto-MapPage-div-049">
          <div style={{
          color: '#ffffff',
          fontSize: '12px',
          fontWeight: 'bold',
          marginBottom: '8px'
        }} data-test="auto-MapPage-div-050">
            World Info
          </div>
          <div style={{
          color: '#8c91a0',
          fontSize: '11px'
        }} data-test="auto-MapPage-div-051">
            <div data-test="auto-MapPage-div-052">Bounds: {mapConfig.width}x{mapConfig.height}</div>
            <div data-test="auto-MapPage-div-053">X: {mapConfig.minX} to {mapConfig.maxX}</div>
            <div data-test="auto-MapPage-div-054">Y: {mapConfig.minY} to {mapConfig.maxY}</div>
            <div style={{
            marginTop: '8px',
            color: '#8b5cf6'
          }} data-test="auto-MapPage-div-055">
              Source: vrising.gaming.tools
            </div>
          </div>
        </div>

        {/* Player List */}
        <div style={{
        marginTop: '24px'
      }} data-test="auto-MapPage-div-056">
          <div style={{
          color: '#ffffff',
          fontSize: '14px',
          fontWeight: 'bold',
          marginBottom: '8px',
          borderBottom: '1px solid #383c4a',
          paddingBottom: '8px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }} data-test="auto-MapPage-div-057">
            <span data-test="auto-MapPage-span-058">Players ({players.length})</span>
            <button onClick={fetchPlayers} style={{
            background: '#383c4a',
            border: '1px solid rgba(0,0,0,0.5)',
            borderRadius: '4px',
            color: '#ffffff',
            cursor: 'pointer',
            fontSize: '10px',
            padding: '4px 8px'
          }} data-test="auto-MapPage-button-059">
              Refresh
            </button>
          </div>
          {players.length === 0 ? <div style={{
          color: '#666',
          fontSize: '11px',
          textAlign: 'center',
          padding: '10px'
        }} data-test="auto-MapPage-div-060">
              No players on map
            </div> : players.map(player => <div key={player.id} onClick={() => handleMarkerClick({
          id: player.id,
          x: player.x,
          y: player.y,
          type: 'player',
          name: player.name,
          data: {
            hp: player.hp,
            maxHp: player.maxHp,
            guild: player.guild,
            zone: player.zone
          }
        })} style={{
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
          padding: '8px',
          background: 'rgba(255,255,255,0.05)',
          borderRadius: '4px',
          marginBottom: '4px',
          cursor: 'pointer'
        }} data-test="auto-MapPage-div-061">
                <div style={{
            width: '8px',
            height: '8px',
            borderRadius: '50%',
            background: player.guild ? '#f59e0b' : '#4ade80'
          }} data-test="auto-MapPage-div-062" />
                <span style={{
            color: '#ffffff',
            fontSize: '12px'
          }} data-test="auto-MapPage-span-063">{player.name}</span>
                {player.guild && <span style={{
            color: '#8b5cf6',
            fontSize: '10px'
          }} data-test="auto-MapPage-span-064">[{player.guild}]</span>}
                <span style={{
            color: '#8c91a0',
            fontSize: '10px',
            marginLeft: 'auto'
          }} data-test="auto-MapPage-span-065">
                  ({player.x.toFixed(0)}, {player.y.toFixed(0)})
                </span>
              </div>)}
        </div>
      </div>
    </div>;
};

// Color helper
const COLORS: Record<string, string> = {
  player: '#4ade80',
  zone: '#3b82f6',
  trap: '#ef4444',
  spawn: '#22c55e',
  arena: '#f59e0b',
  glow: '#8b5cf6'
};
export default MapPage;