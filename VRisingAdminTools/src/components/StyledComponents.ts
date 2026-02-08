
import { styled } from 'goober';

export const BrowserContainer = styled('div')`
  width: 100vw;
  height: 100vh;
  background-color: #1a1a2e;
  color: #ffffff;
  display: flex;
  flex-direction: column;
  overflow: hidden;
`;

export const FlexRow = styled('div')<{ gap?: number; justify?: string; align?: string }>`
  display: flex;
  flex-direction: row;
  gap: ${({ gap }) => gap || 0}px;
  justify-content: ${({ justify }) => justify || 'flex-start'};
  align-items: ${({ align }) => align || 'center'};
`;

export const FlexColumn = styled('div')<{ gap?: number; justify?: string; align?: string }>`
  display: flex;
  flex-direction: column;
  gap: ${({ gap }) => gap || 0}px;
  justify-content: ${({ justify }) => justify || 'flex-start'};
  align-items: ${({ align }) => align || 'stretch'};
`;

export const Grid = styled('div')<{ columns?: number; gap?: number }>`
  display: grid;
  grid-template-columns: repeat(${({ columns }) => columns || 1}, 1fr);
  gap: ${({ gap }) => gap || 0}px;
`;

export const Card = styled('div')`
  background-color: #16213e;
  border-radius: 8px;
  padding: 16px;
  border: 1px solid #383c4a;
`;

export const CardHeader = styled('div')`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  border-bottom: 1px solid #383c4a;
  padding-bottom: 8px;
`;

export const CardTitle = styled('h3')`
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #ffffff;
`;

export const Text = styled('span')<{ size?: number; weight?: number; color?: string }>`
  font-size: ${({ size }) => size || 14}px;
  font-weight: ${({ weight }) => weight || 400};
  color: ${({ color }) => color || '#ffffff'};
`;

export const ComponentBrowserButton = styled('button')`
  background-color: #383c4a;
  color: #ffffff;
  border: 1px solid #4a9eff;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s ease;

  &:hover {
    background-color: #4a9eff;
  }

  &:active {
    transform: translateY(1px);
  }
`;

export const ComponentBrowserInput = styled('input')`
  background-color: #383c4a;
  color: #8c91a0;
  border: 1px solid rgba(0,0,0,0.5);
  padding: 6px 12px;
  border-radius: 4px;
  
  &:focus {
    outline: none;
    border-color: #4a9eff;
    color: #ffffff;
  }
`;

export const StatusIndicator = styled('span')<{ status: 'online' | 'offline' }>`
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;
  color: ${({ status }) => status === 'online' ? '#00ff88' : '#ff4444'};
  
  &::before {
    content: '';
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background-color: ${({ status }) => status === 'online' ? '#00ff88' : '#ff4444'};
  }
`;

export const Collapse = styled('div')<{ opened: string }>`
  height: ${({ opened }) => (opened === "true" ? "auto" : "0px")};
  overflow: ${({ opened }) => (opened === "true" ? "initial" : "hidden")};
`;
