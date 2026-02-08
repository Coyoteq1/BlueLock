
import { ipcMain } from 'electron';

export function setupIpcHandlers() {
  ipcMain.handle('get-app-version', () => {
    return require('../../package.json').version;
  });

  // Add more IPC handlers here as needed
}
