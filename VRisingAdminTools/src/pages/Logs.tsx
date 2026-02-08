
import React from 'react';
import { Card, CardHeader, CardTitle, FlexColumn, Text } from '../components/StyledComponents';

interface PageProps {
  serverUrl: string;
  apiKey: string;
}

export default function Logs({ serverUrl }: PageProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Event Logs</CardTitle>
      </CardHeader>
      <FlexColumn gap={16}>
        <Text>Logs viewer coming soon. Connected to {serverUrl}</Text>
      </FlexColumn>
    </Card>
  );
}
