import React, { useEffect, useState } from 'react';
import { Card, CardHeader, CardTitle, Grid, FlexRow, FlexColumn, Text, ComponentBrowserButton, ComponentBrowserInput, ComponentBrowserSelect, StatusIndicator } from '../components/StyledComponents';
import { api } from '../services/api';
interface ZoneManagementProps {
  serverUrl: string;
  apiKey: string;
}
interface ZoneData {
  id: string;
  name: string;
  center: {
    x: number;
    y: number;
    z: number;
  };
  radius: number;
  isActive: boolean;
  glowEnabled: boolean;
  glowPrefab: string;
  spacing: number;
}
export default function ZoneManagement({
  serverUrl,
  apiKey
}: ZoneManagementProps) {
  const [zones, setZones] = useState<ZoneData[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedZone, setSelectedZone] = useState<ZoneData | null>(null);
  const [glowSpacing, setGlowSpacing] = useState(5);
  const [glowPrefab, setGlowPrefab] = useState('Default');
  useEffect(() => {
    const fetchZones = async () => {
      try {
        const data = await api.getZones(serverUrl, apiKey);
        setZones(data);
      } catch (error) {
        console.error('Failed to fetch zones:', error);
      } finally {
        setLoading(false);
      }
    };
    fetchZones();
  }, [serverUrl, apiKey]);
  const handleSpawnGlows = async () => {
    const success = await api.spawnGlows(serverUrl, apiKey);
    alert(success ? 'Glow borders spawned!' : 'Failed to spawn glow borders.');
  };
  const handleClearGlows = async () => {
    const success = await api.clearGlows(serverUrl, apiKey);
    alert(success ? 'Glow borders cleared!' : 'Failed to clear glow borders.');
  };
  const handleToggleBorders = async (enabled: boolean) => {
    const success = await api.toggleBorders(serverUrl, apiKey, enabled);
    alert(success ? `Borders ${enabled ? 'enabled' : 'disabled'}!` : 'Failed to toggle borders.');
  };
  if (loading) {
    return <Text>Loading zones...</Text>;
  }
  return <FlexColumn gap={24}>
      <Card>
        <CardHeader>
          <CardTitle>Glow Border Controls</CardTitle>
        </CardHeader>
        <FlexRow gap={12}>
          <ComponentBrowserButton onClick={handleSpawnGlows}>Spawn Glows</ComponentBrowserButton>
          <ComponentBrowserButton onClick={handleClearGlows}>Clear Glows</ComponentBrowserButton>
          <ComponentBrowserButton onClick={() => handleToggleBorders(true)}>Enable</ComponentBrowserButton>
          <ComponentBrowserButton onClick={() => handleToggleBorders(false)}>Disable</ComponentBrowserButton>
        </FlexRow>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Configuration</CardTitle>
        </CardHeader>
        <Grid columns={3} gap={16}>
          <div data-test="auto-ZoneManagement-div-001">
            <label style={{
            fontSize: '12px',
            color: '#8c91a0',
            display: 'block',
            marginBottom: '4px'
          }} data-test="auto-ZoneManagement-label-002">
              Glow Spacing (meters)
            </label>
            <ComponentBrowserInput type="number" value={glowSpacing} onChange={e => setGlowSpacing(parseFloat(e.target.value))} min={1} max={20} />
          </div>
          <div data-test="auto-ZoneManagement-div-003">
            <label style={{
            fontSize: '12px',
            color: '#8c91a0',
            display: 'block',
            marginBottom: '4px'
          }} data-test="auto-ZoneManagement-label-004">
              Glow Prefab
            </label>
            <ComponentBrowserSelect value={glowPrefab} onChange={e => setGlowPrefab(e.target.value)}>
              <option value="Default" data-test="auto-ZoneManagement-option-005">Default</option>
              <option value="Blue" data-test="auto-ZoneManagement-option-006">Blue</option>
              <option value="Red" data-test="auto-ZoneManagement-option-007">Red</option>
              <option value="Green" data-test="auto-ZoneManagement-option-008">Green</option>
            </ComponentBrowserSelect>
          </div>
        </Grid>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Arena Zones ({zones.length})</CardTitle>
        </CardHeader>
        {zones.length === 0 ? <Text color="#6c7080">No zones configured.</Text> : <Grid columns={2} gap={12}>
            {zones.map(zone => <div key={zone.id} onClick={() => setSelectedZone(zone)} style={{
          padding: '12px',
          background: 'rgba(56, 60, 74, 0.3)',
          borderRadius: '8px',
          cursor: 'pointer',
          border: selectedZone?.id === zone.id ? '1px solid #4a9eff' : '1px solid transparent'
        }} data-test="auto-ZoneManagement-div-009">
                <FlexRow justify="space-between">
                  <FlexColumn gap={4}>
                    <Text weight={600} color="#ffffff">{zone.name}</Text>
                    <Text size={11} color="#6c7080">
                      Center: ({zone.center.x}, {zone.center.y}, {zone.center.z})
                    </Text>
                  </FlexColumn>
                  <StatusIndicator status={zone.isActive ? 'online' : 'offline'}>
                    {zone.isActive ? 'Active' : 'Inactive'}
                  </StatusIndicator>
                </FlexRow>
              </div>)}
          </Grid>}
      </Card>
    </FlexColumn>;
}