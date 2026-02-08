
import React from 'react';
import { Card, CardHeader, CardTitle, FlexColumn, Text } from '../components/StyledComponents';

interface PageProps {
  serverUrl: string;
  apiKey: string;
}

export default function Configuration({ serverUrl }: PageProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Configuration</CardTitle>
      </CardHeader>
      <FlexColumn gap={16}>
        <Text>Configuration editor coming soon. Connected to {serverUrl}</Text>
      </FlexColumn>
    </Card>
  );
}
