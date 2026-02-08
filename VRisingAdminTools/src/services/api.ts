
import axios from 'axios';

const getHeaders = (apiKey: string) => ({
  'Content-Type': 'application/json',
  'X-API-Key': apiKey
});

export const api = {
  getServerStatus: async (url: string, apiKey: string) => {
    try {
      const response = await axios.get(`${url}/api/v1/status`, { headers: getHeaders(apiKey) });
      return response.data.data;
    } catch (error) {
      console.error('Error fetching server status:', error);
      return { online: false, playerCount: 0, maxPlayers: 0, uptime: 0 };
    }
  },
  
  getQuickStats: async (url: string, apiKey: string) => {
    try {
      const response = await axios.get(`${url}/api/v1/stats`, { headers: getHeaders(apiKey) });
      return response.data.data;
    } catch (error) {
      console.error('Error fetching quick stats:', error);
      return { activeZones: 0, totalTraps: 0, armedTraps: 0, activeChests: 0, activeStreaks: 0 };
    }
  },

  spawnGlows: async (url: string, apiKey: string) => {
    return axios.post(`${url}/api/v1/zones/glow/spawn`, {}, { headers: getHeaders(apiKey) });
  },

  clearGlows: async (url: string, apiKey: string) => {
    return axios.post(`${url}/api/v1/zones/glow/clear`, {}, { headers: getHeaders(apiKey) });
  },

  reloadConfig: async (url: string, apiKey: string) => {
    return axios.post(`${url}/api/v1/config/reload`, {}, { headers: getHeaders(apiKey) });
  },

  clearAllTraps: async (url: string, apiKey: string) => {
    return axios.post(`${url}/api/v1/traps/clear`, {}, { headers: getHeaders(apiKey) });
  },

  clearAllChests: async (url: string, apiKey: string) => {
    return axios.post(`${url}/api/v1/chests/clear`, {}, { headers: getHeaders(apiKey) });
  },
  
  getZones: async (url: string, apiKey: string) => {
      const response = await axios.get(`${url}/api/v1/zones`, { headers: getHeaders(apiKey) });
      return response.data.data;
  },

  getTraps: async (url: string, apiKey: string) => {
      const response = await axios.get(`${url}/api/v1/traps`, { headers: getHeaders(apiKey) });
      return response.data.data;
  },
  
  getConfig: async (url: string, apiKey: string) => {
      const response = await axios.get(`${url}/api/v1/config`, { headers: getHeaders(apiKey) });
      return response.data.data;
  },
  
  getLogs: async (url: string, apiKey: string) => {
      const response = await axios.get(`${url}/api/v1/logs`, { headers: getHeaders(apiKey) });
      return response.data.data;
  }
};
