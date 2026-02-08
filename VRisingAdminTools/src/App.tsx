import React, { useState } from 'react';
import { setup } from 'goober';
import { BrowserContainer } from './components/StyledComponents';
import Dashboard from './pages/Dashboard';
import ZoneManagement from './pages/ZoneManagement';
import TrapSystem from './pages/TrapSystem';
import Configuration from './pages/Configuration';
import Logs from './pages/Logs';
import MapPage from './pages/MapPage';

// Setup goober to use React
setup(React.createElement);

type Tab = 'dashboard' | 'zones' | 'traps' | 'config' | 'logs' | 'map';

function App() {
  const [activeTab, setActiveTab] = useState<Tab>('dashboard');
  const [serverUrl, setServerUrl] = useState('http://localhost:8080');
  const [apiKey, setApiKey] = useState('');

  const tabs: { id: Tab; label: string }[] = [
    { id: 'dashboard', label: 'Dashboard' },
    { id: 'zones', label: 'Zones' },
    { id: 'traps', label: 'Traps' },
    { id: 'map', label: 'Map' },
    { id: 'config', label: 'Config' },
    { id: 'logs', label: 'Logs' },
  ];

  const renderContent = () => {
    switch (activeTab) {
      case 'dashboard':
        return <Dashboard serverUrl={serverUrl} apiKey={apiKey} />;
      case 'zones':
        return <ZoneManagement serverUrl={serverUrl} apiKey={apiKey} />;
      case 'traps':
        return <TrapSystem serverUrl={serverUrl} apiKey={apiKey} />;
      case 'map':
        return <MapPage serverUrl={serverUrl} apiKey={apiKey} />;
      case 'config':
        return <Configuration serverUrl={serverUrl} apiKey={apiKey} />;
      case 'logs':
        return <Logs serverUrl={serverUrl} apiKey={apiKey} />;
      default:
        return <Dashboard serverUrl={serverUrl} apiKey={apiKey} />;
    }
  };

  return (
    <BrowserContainer className="app-container">
      {/* Header */}
      <header style={{
        padding: '16px 24px',
        background: 'rgba(0,0,0,0.3)',
        borderBottom: '1px solid #383c4a',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between'
      }}>
        <h1 style={{ fontSize: '20px', fontWeight: 600, color: '#ffffff' }}>
          V Rising Admin Tools
        </h1>
        
        {/* Server Connection */}
        <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
          <input
            type="text"
            placeholder="Server URL"
            value={serverUrl}
            onChange={(e) => setServerUrl(e.target.value)}
            style={{
              background: '#383c4a',
              border: '1px solid rgba(0,0,0,0.5)',
              borderRadius: '4px',
              padding: '6px 12px',
              color: '#8c91a0',
              width: '200px'
            }}
          />
          <input
            type="password"
            placeholder="API Key"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            style={{
              background: '#383c4a',
              border: '1px solid rgba(0,0,0,0.5)',
              borderRadius: '4px',
              padding: '6px 12px',
              color: '#8c91a0',
              width: '150px'
            }}
          />
        </div>
      </header>

      {/* Navigation */}
      <nav style={{
        display: 'flex',
        gap: '4px',
        padding: '8px 24px',
        background: 'rgba(0,0,0,0.2)',
        borderBottom: '1px solid #383c4a'
      }}>
        {tabs.map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              padding: '8px 16px',
              borderRadius: '4px',
              border: 'none',
              cursor: 'pointer',
              fontSize: '14px',
              fontWeight: 500,
              background: activeTab === tab.id ? '#4a9eff' : 'transparent',
              color: activeTab === tab.id ? '#ffffff' : '#8c91a0',
              transition: 'all 0.2s ease'
            }}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {/* Main Content */}
      <main style={{ flex: 1, overflow: 'auto', padding: '24px' }}>
        {renderContent()}
      </main>

      {/* Footer */}
      <footer style={{
        padding: '8px 24px',
        background: 'rgba(0,0,0,0.3)',
        borderTop: '1px solid #383c4a',
        fontSize: '12px',
        color: '#8c91a0',
        textAlign: 'center'
      }}>
        V Rising Admin Tools v1.0.0 | Connected to {serverUrl}
      </footer>
    </BrowserContainer>
  );
}

export default App;
