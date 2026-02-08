import React, { useEffect, useState } from 'react';
import { Grid, Card, CardHeader, CardTitle, StatusIndicator, FlexRow, FlexColumn, Text, ComponentBrowserButton } from '../components/StyledComponents';
import { api } from '../services/api';

interface DashboardProps {
  serverUrl: string;
  apiKey: string;
}

interface ServerStatus {
  online: boolean;
  playerCount: number;
  maxPlayers: number;
  uptime: number;
}

interface QuickStats {
  activeZones: number;
  totalTraps: number;
  armedTraps: number;
  activeChests: number;
  activeStreaks: number;
}

export default function Dashboard({ serverUrl, apiKey }: DashboardProps) {
  const [status, setStatus] = useState<ServerStatus | null>(null);
  const [stats, setStats] = useState<QuickStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [statusData, statsData] = await Promise.all([
          api.getServerStatus(serverUrl, apiKey),
          api.getQuickStats(serverUrl, apiKey)
        ]);
        setStatus(statusData);
        setStats(statsData);
      } catch (error) {
        console.error('Failed to fetch dashboard data:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
    const interval = setInterval(fetchData, 5000);
    return () => clearInterval(interval);
  }, [serverUrl, apiKey]);

  if (loading) {
    return (
      <FlexColumn gap={16}>
        <Text>Loading dashboard...</Text>
      </FlexColumn>
    );
  }

  const formatUptime = (seconds: number) => {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return `${hours}h ${minutes}m`;
  };

  return (
    <FlexColumn gap={24}>
      {/* Server Status */}
      <Card>
        <CardHeader>
          <CardTitle>Server Status</CardTitle>
          <StatusIndicator status={status?.online ? 'online' : 'offline'}>
            {status?.online ? 'Online' : 'Offline'}
          </StatusIndicator>
        </CardHeader>
        <Grid columns={4} gap={24}>
          <FlexColumn gap={4}>
            <Text color="#6c7080" size={12}>Players</Text>
            <Text size={24} weight={600} color="#ffffff">
              {status?.playerCount || 0} / {status?.maxPlayers || 0}
            </Text>
          </FlexColumn>
          <FlexColumn gap={4}>
            <Text color="#6c7080" size={12}>Uptime</Text>
            <Text size={24} weight={600} color="#ffffff">
              {formatUptime(status?.uptime || 0)}
            </Text>
          </FlexColumn>
          <FlexColumn gap={4}>
            <Text color="#6c7080" size={12}>Active Zones</Text>
            <Text size={24} weight={600} color="#ffffff">
              {stats?.activeZones || 0}
            </Text>
          </FlexColumn>
          <FlexColumn gap={4}>
            <Text color="#6c7080" size={12}>Total Traps</Text>
            <Text size={24} weight={600} color="#ffffff">
              {stats?.totalTraps || 0}
            </Text>
          </FlexColumn>
        </Grid>
      </Card>

      {/* Quick Actions */}
      <Card>
        <CardHeader>
          <CardTitle>Quick Actions</CardTitle>
        </CardHeader>
        <FlexRow gap={12}>
          <ComponentBrowserButton onClick={() => api.spawnGlows(serverUrl, apiKey)}>
            Spawn Glows
          </ComponentBrowserButton>
          <ComponentBrowserButton onClick={() => api.clearGlows(serverUrl, apiKey)}>
            Clear Glows
          </ComponentBrowserButton>
          <ComponentBrowserButton onClick={() => api.reloadConfig(serverUrl, apiKey)}>
            Reload Config
          </ComponentBrowserButton>
          <ComponentBrowserButton onClick={() => api.clearAllTraps(serverUrl, apiKey)}>
            Clear Traps
          </ComponentBrowserButton>
          <ComponentBrowserButton onClick={() => api.clearAllChests(serverUrl, apiKey)}>
            Clear Chests
          </ComponentBrowserButton>
        </FlexRow>
      </Card>

      {/* System Overview */}
      <Grid columns={3} gap={16}>
        <Card>
          <CardHeader>
            <CardTitle>Trap System</CardTitle>
          </CardHeader>
          <FlexColumn gap={12}>
            <FlexRow justify="space-between">
              <Text>Total Traps</Text>
              <Text color="#4a9eff" weight={600}>{stats?.totalTraps || 0}</Text>
            </FlexRow>
            <FlexRow justify="space-between">
              <Text>Armed</Text>
              <Text color="#00ff88" weight={600}>{stats?.armedTraps || 0}</Text>
            </FlexRow>
            <FlexRow justify="space-between">
              <Text>Active Chests</Text>
              <Text color="#ffaa00" weight={600}>{stats?.activeChests || 0}</Text>
            </FlexRow>
            <FlexRow justify="space-between">
              <Text>Kill Streaks</Text>
              <Text color="#ff4444" weight={600}>{stats?.activeStreaks || 0}</Text>
            </FlexRow>
          </FlexColumn>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Zone Status</CardTitle>
          </CardHeader>
          <FlexColumn gap={12}>
            <FlexRow justify="space-between">
              <Text>Active Zones</Text>
              <Text color="#00ff88" weight={600}>{stats?.activeZones || 0}</Text>
            </FlexRow>
            <FlexRow justify="space-between">
              <Text>Glow Borders</Text>
              <Text color="#4a9eff" weight={600}>Enabled</Text>
            </FlexRow>
            <FlexRow justify="space-between">
              <Text>Corner Spawns</Text>
              <Text color="#8c91a0" weight={600}>Enabled</Text>
            </FlexRow>
          </FlexColumn>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Connection</CardTitle>
          </CardHeader>
          <FlexColumn gap={12}>
            <FlexRow justify="space-between">
              <Text>Server URL</Text>
              <Text color="#8c91a0" size={12}>{serverUrl}</Text>
            </FlexRow>
            <FlexRow justify="space-between">
              <Text>API Status</Text>
              <StatusIndicator status="online">Connected</StatusIndicator>
            </FlexRow>
            <FlexRow justify="space-between">
              <Text>Auto-refresh</Text>
              <Text color="#00ff88" weight={600}>5s</Text>
            </FlexRow>
          </FlexColumn>
        </Card>
      </Grid>
    </FlexColumn>
  );
}
