import React, { useEffect, useState } from 'react';
import { Card, CardHeader, CardTitle, Grid, FlexRow, FlexColumn, Text, ComponentBrowserButton, ComponentBrowserInput, ComponentBrowserSelect, StatusIndicator, Badge } from '../components/StyledComponents';
import { api } from '../services/api';
interface TrapSystemProps {
  serverUrl: string;
  apiKey: string;
}
interface TrapData {
  position: {
    x: number;
    y: number;
    z: number;
  };
  isArmed: boolean;
  triggered: boolean;
  ownerId: string;
}
interface TrapZone {
  position: {
    x: number;
    y: number;
    z: number;
  };
  type: string;
  radius: number;
  isArmed: boolean;
  triggered: boolean;
}
interface ChestData {
  position: {
    x: number;
    y: number;
    z: number;
  };
  type: string;
}
export default function TrapSystem({
  serverUrl,
  apiKey
}: TrapSystemProps) {
  const [activeTab, setActiveTab] = useState<'traps' | 'zones' | 'chests' | 'streaks'>('traps');
  const [traps, setTraps] = useState<TrapData[]>([]);
  const [zones, setZones] = useState<TrapZone[]>([]);
  const [chests, setChests] = useState<ChestData[]>([]);
  const [streaks, setStreaks] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const fetchData = async () => {
      try {
        const [trapsData, zonesData, chestsData, streaksData] = await Promise.all([api.getTraps(serverUrl, apiKey), api.getTrapZones(serverUrl, apiKey), api.getChests(serverUrl, apiKey), api.getStreaks(serverUrl, apiKey)]);
        setTraps(trapsData);
        setZones(zonesData);
        setChests(chestsData);
        setStreaks(streaksData);
      } catch (error) {
        console.error('Failed to fetch trap data:', error);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [serverUrl, apiKey]);
  const handleClearAllTraps = async () => {
    if (confirm('Are you sure you want to clear all traps?')) {
      const success = await api.clearAllTraps(serverUrl, apiKey);
      if (success) {
        setTraps([]);
        alert('All traps cleared!');
      } else {
        alert('Failed to clear traps.');
      }
    }
  };
  const handleClearAllChests = async () => {
    if (confirm('Are you sure you want to clear all chests?')) {
      const success = await api.clearAllChests(serverUrl, apiKey);
      if (success) {
        setChests([]);
        alert('All chests cleared!');
      } else {
        alert('Failed to clear chests.');
      }
    }
  };
  const handleRemoveTrap = async (position: {
    x: number;
    y: number;
    z: number;
  }) => {
    const success = await api.removeTrap(serverUrl, apiKey, position);
    if (success) {
      setTraps(traps.filter(t => t.position.x !== position.x || t.position.y !== position.y || t.position.z !== position.z));
    }
  };
  const handleArmTrap = async (position: {
    x: number;
    y: number;
    z: number;
  }, armed: boolean) => {
    await api.armTrap(serverUrl, apiKey, position, armed);
    setTraps(traps.map(t => {
      if (t.position.x === position.x && t.position.y === position.y && t.position.z === position.z) {
        return {
          ...t,
          isArmed: armed
        };
      }
      return t;
    }));
  };
  const handleRemoveChest = async (position: {
    x: number;
    y: number;
    z: number;
  }) => {
    const success = await api.removeChest(serverUrl, apiKey, position);
    if (success) {
      setChests(chests.filter(c => c.position.x !== position.x || c.position.y !== position.y || c.position.z !== position.z));
    }
  };
  const handleResetStreak = async (platformId: string) => {
    const success = await api.resetStreak(serverUrl, apiKey, platformId);
    if (success) {
      setStreaks(streaks.filter(s => s.platformId !== platformId));
    }
  };
  const getChestBadgeColor = (type: string) => {
    switch (type.toLowerCase()) {
      case 'legendary':
        return '#ffaa00';
      case 'epic':
        return '#a855f7';
      case 'rare':
        return '#3b82f6';
      default:
        return '#22c55e';
    }
  };
  if (loading) {
    return <Text>Loading trap system...</Text>;
  }
  return <FlexColumn gap={24}>
      {/* Tab Navigation */}
      <FlexRow gap={8}>
        {(['traps', 'zones', 'chests', 'streaks'] as const).map(tab => <ComponentBrowserButton key={tab} active={activeTab === tab} onClick={() => setActiveTab(tab)}>
            {tab.charAt(0).toUpperCase() + tab.slice(1)}
          </ComponentBrowserButton>)}
      </FlexRow>

      {/* Traps Tab */}
      {activeTab === 'traps' && <>
          <Card>
            <CardHeader>
              <CardTitle>Container Traps ({traps.length})</CardTitle>
              <ComponentBrowserButton onClick={handleClearAllTraps}>
                Clear All
              </ComponentBrowserButton>
            </CardHeader>
            
            {traps.length === 0 ? <Text color="#6c7080">No traps configured.</Text> : <Grid columns={3} gap={12}>
                {traps.map((trap, index) => <div key={index} style={{
            padding: '12px',
            background: 'rgba(56, 60, 74, 0.3)',
            borderRadius: '8px'
          }} data-test="auto-TrapSystem-div-001">
                    <FlexRow justify="space-between" align="start">
                      <FlexColumn gap={4}>
                        <FlexRow gap={8} align="center">
                          <StatusIndicator status={trap.isArmed ? 'online' : 'offline'}>
                            {trap.isArmed ? 'Armed' : 'Disarmed'}
                          </StatusIndicator>
                          {trap.triggered && <Badge color="#ff4444">Triggered</Badge>}
                        </FlexRow>
                        <Text size={11} color="#6c7080">
                          ({trap.position.x}, {trap.position.y}, {trap.position.z})
                        </Text>
                      </FlexColumn>
                      <FlexRow gap={4}>
                        <ComponentBrowserButton onClick={() => handleArmTrap(trap.position, !trap.isArmed)} style={{
                  padding: '4px 8px',
                  fontSize: '11px'
                }}>
                          {trap.isArmed ? 'Disarm' : 'Arm'}
                        </ComponentBrowserButton>
                        <ComponentBrowserButton onClick={() => handleRemoveTrap(trap.position)} style={{
                  padding: '4px 8px',
                  fontSize: '11px'
                }}>
                          Remove
                        </ComponentBrowserButton>
                      </FlexRow>
                    </FlexRow>
                  </div>)}
              </Grid>}
          </Card>
        </>}

      {/* Zones Tab */}
      {activeTab === 'zones' && <Card>
          <CardHeader>
            <CardTitle>Trap Zones ({zones.length})</CardTitle>
          </CardHeader>
          
          {zones.length === 0 ? <Text color="#6c7080">No trap zones configured.</Text> : <Grid columns={2} gap={12}>
              {zones.map((zone, index) => <div key={index} style={{
          padding: '12px',
          background: 'rgba(56, 60, 74, 0.3)',
          borderRadius: '8px'
        }} data-test="auto-TrapSystem-div-002">
                  <FlexRow justify="space-between" align="start">
                    <FlexColumn gap={4}>
                      <FlexRow gap={8} align="center">
                        <Badge color="#4a9eff">{zone.type}</Badge>
                        <StatusIndicator status={zone.isArmed ? 'online' : 'offline'}>
                          {zone.isArmed ? 'Armed' : 'Disarmed'}
                        </StatusIndicator>
                      </FlexRow>
                      <Text size={11} color="#6c7080">
                        Radius: {zone.radius}m | Pos: ({zone.position.x}, {zone.position.y}, {zone.position.z})
                      </Text>
                    </FlexColumn>
                  </FlexRow>
                </div>)}
            </Grid>}
        </Card>}

      {/* Chests Tab */}
      {activeTab === 'chests' && <Card>
          <CardHeader>
            <CardTitle>Spawned Chests ({chests.length})</CardTitle>
            <ComponentBrowserButton onClick={handleClearAllChests}>
              Clear All
            </ComponentBrowserButton>
          </CardHeader>
          
          {chests.length === 0 ? <Text color="#6c7080">No chests spawned.</Text> : <Grid columns={3} gap={12}>
              {chests.map((chest, index) => <div key={index} style={{
          padding: '12px',
          background: 'rgba(56, 60, 74, 0.3)',
          borderRadius: '8px'
        }} data-test="auto-TrapSystem-div-003">
                  <FlexRow justify="space-between" align="center">
                    <FlexColumn gap={4}>
                      <Badge color={getChestBadgeColor(chest.type)}>
                        {chest.type}
                      </Badge>
                      <Text size={11} color="#6c7080">
                        ({chest.position.x}, {chest.position.y}, {chest.position.z})
                      </Text>
                    </FlexColumn>
                    <ComponentBrowserButton onClick={() => handleRemoveChest(chest.position)} style={{
              padding: '4px 8px',
              fontSize: '11px'
            }}>
                      Remove
                    </ComponentBrowserButton>
                  </FlexRow>
                </div>)}
            </Grid>}
        </Card>}

      {/* Streaks Tab */}
      {activeTab === 'streaks' && <Card>
          <CardHeader>
            <CardTitle>Active Kill Streaks ({streaks.length})</CardTitle>
          </CardHeader>
          
          {streaks.length === 0 ? <Text color="#6c7080">No active kill streaks.</Text> : <Grid columns={2} gap={12}>
              {streaks.map((streak, index) => <div key={index} style={{
          padding: '12px',
          background: 'rgba(56, 60, 74, 0.3)',
          borderRadius: '8px'
        }} data-test="auto-TrapSystem-div-004">
                  <FlexRow justify="space-between" align="center">
                    <FlexColumn gap={4}>
                      <Text weight={600} color="#ffffff">
                        {streak.playerName || 'Unknown'}
                      </Text>
                      <Text size={11} color="#6c7080">
                        {streak.kills} kills | Platform ID: {streak.platformId}
                      </Text>
                    </FlexColumn>
                    <ComponentBrowserButton onClick={() => handleResetStreak(streak.platformId)} style={{
              padding: '4px 8px',
              fontSize: '11px'
            }}>
                      Reset
                    </ComponentBrowserButton>
                  </FlexRow>
                </div>)}
            </Grid>}
        </Card>}
    </FlexColumn>;
}