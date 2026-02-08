import React, { useRef, useEffect, useState, useCallback } from 'react';
export interface MapMarker {
  id: string;
  x: number;
  y: number;
  type: 'player' | 'zone' | 'trap' | 'spawn' | 'arena';
  name: string;
  data?: Record<string, unknown>;
}
export interface MapZone {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
  type: 'arena' | 'spawn' | 'trap' | 'glow';
  name: string;
  color?: string;
}
interface InteractiveMapProps {
  markers?: MapMarker[];
  zones?: MapZone[];
  width?: number;
  height?: number;
  onMarkerClick?: (marker: MapMarker) => void;
  onZoneClick?: (zone: MapZone) => void;
}
const DEFAULT_ZONES: MapZone[] = [{
  id: 'arena-main',
  x: 0,
  y: 0,
  width: 200,
  height: 200,
  type: 'arena',
  name: 'Main Arena'
}, {
  id: 'spawn-north',
  x: -300,
  y: -200,
  width: 100,
  height: 100,
  type: 'spawn',
  name: 'North Spawn'
}, {
  id: 'spawn-south',
  x: 200,
  y: 300,
  width: 100,
  height: 100,
  type: 'spawn',
  name: 'South Spawn'
}];
const DEFAULT_MARKERS: MapMarker[] = [{
  id: 'p1',
  x: 50,
  y: 50,
  type: 'player',
  name: 'Player 1'
}, {
  id: 'p2',
  x: -50,
  y: 100,
  type: 'player',
  name: 'Player 2'
}, {
  id: 'p3',
  x: 100,
  y: -50,
  type: 'player',
  name: 'Player 3'
}];
const COLORS: Record<string, string> = {
  player: '#4ade80',
  zone: '#3b82f6',
  trap: '#ef4444',
  spawn: '#22c55e',
  arena: '#f59e0b',
  glow: '#8b5cf6'
};
export const InteractiveMap: React.FC<InteractiveMapProps> = ({
  markers = DEFAULT_MARKERS,
  zones = DEFAULT_ZONES,
  width = 800,
  height = 600,
  onMarkerClick,
  onZoneClick
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(1);
  const [offset, setOffset] = useState({
    x: 0,
    y: 0
  });
  const [hoveredMarker, setHoveredMarker] = useState<MapMarker | null>(null);
  const [hoveredZone, setHoveredZone] = useState<MapZone | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [dragStart, setDragStart] = useState({
    x: 0,
    y: 0
  });

  // Draw the map
  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Clear canvas
    ctx.fillStyle = '#1a1a2e';
    ctx.fillRect(0, 0, width, height);

    // Draw grid
    ctx.strokeStyle = '#2d2d44';
    ctx.lineWidth = 0.5;
    const gridSize = 50 * scale;
    const startX = offset.x % gridSize;
    const startY = offset.y % gridSize;
    for (let x = startX; x < width; x += gridSize) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, height);
      ctx.stroke();
    }
    for (let y = startY; y < height; y += gridSize) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      ctx.stroke();
    }

    // Apply transformations
    ctx.save();
    ctx.translate(offset.x + width / 2, offset.y + height / 2);
    ctx.scale(scale, scale);

    // Draw zones
    zones.forEach(zone => {
      ctx.fillStyle = zone.color || COLORS[zone.type] + '40';
      ctx.strokeStyle = zone.color || COLORS[zone.type];
      ctx.lineWidth = 2 / scale;

      // Draw zone rectangle
      ctx.fillRect(zone.x - zone.width / 2, zone.y - zone.height / 2, zone.width, zone.height);
      ctx.strokeRect(zone.x - zone.width / 2, zone.y - zone.height / 2, zone.width, zone.height);

      // Draw zone label
      ctx.fillStyle = '#ffffff';
      ctx.font = `${12 / scale}px sans-serif`;
      ctx.textAlign = 'center';
      ctx.fillText(zone.name, zone.x, zone.y);
    });

    // Draw markers
    markers.forEach(marker => {
      ctx.fillStyle = COLORS[marker.type];

      // Draw marker shape based on type
      switch (marker.type) {
        case 'player':
          // Draw circle for players
          ctx.beginPath();
          ctx.arc(marker.x, marker.y, 8 / scale, 0, Math.PI * 2);
          ctx.fill();
          ctx.strokeStyle = '#ffffff';
          ctx.lineWidth = 2 / scale;
          ctx.stroke();
          break;
        case 'trap':
          // Draw triangle for traps
          ctx.beginPath();
          ctx.moveTo(marker.x, marker.y - 10 / scale);
          ctx.lineTo(marker.x + 10 / scale, marker.y + 10 / scale);
          ctx.lineTo(marker.x - 10 / scale, marker.y + 10 / scale);
          ctx.closePath();
          ctx.fill();
          break;
        default:
          // Draw square for other markers
          ctx.fillRect(marker.x - 6 / scale, marker.y - 6 / scale, 12 / scale, 12 / scale);
      }

      // Draw label
      ctx.fillStyle = '#ffffff';
      ctx.font = `bold ${10 / scale}px sans-serif`;
      ctx.textAlign = 'center';
      ctx.fillText(marker.name, marker.x, marker.y + 20 / scale);
    });
    ctx.restore();

    // Draw center crosshair
    ctx.strokeStyle = '#ff000040';
    ctx.lineWidth = 1;
    ctx.setLineDash([5, 5]);
    ctx.beginPath();
    ctx.moveTo(width / 2 - 10, height / 2);
    ctx.lineTo(width / 2 + 10, height / 2);
    ctx.moveTo(width / 2, height / 2 - 10);
    ctx.lineTo(width / 2, height / 2 + 10);
    ctx.stroke();
    ctx.setLineDash([]);
  }, [markers, zones, width, height, scale, offset]);

  // Handle canvas click
  const handleCanvasClick = useCallback((e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const x = (e.clientX - rect.left - offset.x - width / 2) / scale;
    const y = (e.clientY - rect.top - offset.y - height / 2) / scale;

    // Check if clicked on a marker
    const clickedMarker = markers.find(marker => {
      const dist = Math.sqrt(Math.pow(marker.x - x, 2) + Math.pow(marker.y - y, 2));
      return dist < 15 / scale;
    });
    if (clickedMarker && onMarkerClick) {
      onMarkerClick(clickedMarker);
      return;
    }

    // Check if clicked on a zone
    const clickedZone = zones.find(zone => {
      return x >= zone.x - zone.width / 2 && x <= zone.x + zone.width / 2 && y >= zone.y - zone.height / 2 && y <= zone.y + zone.height / 2;
    });
    if (clickedZone && onZoneClick) {
      onZoneClick(clickedZone);
    }
  }, [markers, zones, width, height, scale, offset, onMarkerClick, onZoneClick]);

  // Handle mouse move for hover effects
  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const x = (e.clientX - rect.left - offset.x - width / 2) / scale;
    const y = (e.clientY - rect.top - offset.y - height / 2) / scale;

    // Check for marker hover
    const hovered = markers.find(marker => {
      const dist = Math.sqrt(Math.pow(marker.x - x, 2) + Math.pow(marker.y - y, 2));
      return dist < 15 / scale;
    });
    setHoveredMarker(hovered || null);

    // Check for zone hover
    const zoneHovered = zones.find(zone => {
      return x >= zone.x - zone.width / 2 && x <= zone.x + zone.width / 2 && y >= zone.y - zone.height / 2 && y <= zone.y + zone.height / 2;
    });
    setHoveredZone(zoneHovered || null);
  }, [markers, zones, width, height, scale, offset]);

  // Handle mouse wheel zoom
  const handleWheel = useCallback((e: React.WheelEvent) => {
    e.preventDefault();
    const zoomFactor = e.deltaY > 0 ? 0.9 : 1.1;
    const newScale = Math.min(Math.max(scale * zoomFactor, 0.5), 5);
    setScale(newScale);
  }, [scale]);

  // Handle drag
  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    setIsDragging(true);
    setDragStart({
      x: e.clientX - offset.x,
      y: e.clientY - offset.y
    });
  }, [offset]);
  const handleMouseUp = useCallback(() => {
    setIsDragging(false);
  }, []);
  const handleMouseMoveDrag = useCallback((e: React.MouseEvent) => {
    if (!isDragging) return;
    setOffset({
      x: e.clientX - dragStart.x,
      y: e.clientY - dragStart.y
    });
  }, [isDragging, dragStart]);

  // Redraw when dependencies change
  useEffect(() => {
    draw();
  }, [draw]);

  // Zoom controls
  const handleZoomIn = () => setScale(s => Math.min(s * 1.2, 5));
  const handleZoomOut = () => setScale(s => Math.max(s / 1.2, 0.5));
  const handleReset = () => {
    setScale(1);
    setOffset({
      x: 0,
      y: 0
    });
  };
  return <div ref={containerRef} style={{
    position: 'relative',
    width,
    height
  }} data-test="auto-InteractiveMap-div-001">
      {/* Canvas */}
      <canvas ref={canvasRef} width={width} height={height} onClick={handleCanvasClick} onMouseMove={handleMouseMove} onWheel={handleWheel} onMouseDown={handleMouseDown} onMouseUp={handleMouseUp} onMouseLeave={handleMouseUp} onMouseMoveCapture={handleMouseMoveDrag} style={{
      cursor: isDragging ? 'grabbing' : hoveredMarker ? 'pointer' : 'grab',
      border: '1px solid #383c4a',
      borderRadius: '8px'
    }} data-test="auto-InteractiveMap-canvas-002" />

      {/* Controls */}
      <div style={{
      position: 'absolute',
      top: '10px',
      right: '10px',
      display: 'flex',
      flexDirection: 'column',
      gap: '4px'
    }} data-test="auto-InteractiveMap-div-003">
        <button onClick={handleZoomIn} style={{
        width: '32px',
        height: '32px',
        background: '#383c4a',
        border: '1px solid rgba(0,0,0,0.5)',
        borderRadius: '4px',
        color: '#ffffff',
        cursor: 'pointer',
        fontSize: '18px'
      }} data-test="auto-InteractiveMap-button-004">
          +
        </button>
        <button onClick={handleZoomOut} style={{
        width: '32px',
        height: '32px',
        background: '#383c4a',
        border: '1px solid rgba(0,0,0,0.5)',
        borderRadius: '4px',
        color: '#ffffff',
        cursor: 'pointer',
        fontSize: '18px'
      }} data-test="auto-InteractiveMap-button-005">
          −
        </button>
        <button onClick={handleReset} style={{
        width: '32px',
        height: '32px',
        background: '#383c4a',
        border: '1px solid rgba(0,0,0,0.5)',
        borderRadius: '4px',
        color: '#ffffff',
        cursor: 'pointer',
        fontSize: '12px'
      }} data-test="auto-InteractiveMap-button-006">
          ⌂
        </button>
      </div>

      {/* Legend */}
      <div style={{
      position: 'absolute',
      bottom: '10px',
      left: '10px',
      background: 'rgba(0,0,0,0.7)',
      padding: '8px 12px',
      borderRadius: '4px',
      fontSize: '11px'
    }} data-test="auto-InteractiveMap-div-007">
        <div style={{
        marginBottom: '4px',
        color: '#ffffff',
        fontWeight: 'bold'
      }} data-test="auto-InteractiveMap-div-008">Legend</div>
        {Object.entries(COLORS).map(([type, color]) => <div key={type} style={{
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        margin: '2px 0'
      }} data-test="auto-InteractiveMap-div-009">
            <span style={{
          width: '10px',
          height: '10px',
          background: color,
          borderRadius: '2px'
        }} data-test="auto-InteractiveMap-span-010" />
            <span style={{
          color: '#8c91a0',
          textTransform: 'capitalize'
        }} data-test="auto-InteractiveMap-span-011">{type}</span>
          </div>)}
      </div>

      {/* Scale indicator */}
      <div style={{
      position: 'absolute',
      bottom: '10px',
      right: '10px',
      background: 'rgba(0,0,0,0.7)',
      padding: '4px 8px',
      borderRadius: '4px',
      fontSize: '11px',
      color: '#8c91a0'
    }} data-test="auto-InteractiveMap-div-012">
        Scale: {scale.toFixed(1)}x
      </div>

      {/* Tooltip */}
      {(hoveredMarker || hoveredZone) && <div style={{
      position: 'absolute',
      top: '10px',
      left: '10px',
      background: 'rgba(0,0,0,0.9)',
      padding: '8px 12px',
      borderRadius: '4px',
      border: '1px solid #383c4a',
      maxWidth: '200px'
    }} data-test="auto-InteractiveMap-div-013">
          <div style={{
        color: '#ffffff',
        fontWeight: 'bold'
      }} data-test="auto-InteractiveMap-div-014">
            {hoveredMarker?.name || hoveredZone?.name}
          </div>
          <div style={{
        color: COLORS[hoveredMarker?.type || hoveredZone?.type || ''],
        fontSize: '11px',
        marginTop: '2px'
      }} data-test="auto-InteractiveMap-div-015">
            {hoveredMarker?.type || hoveredZone?.type}
          </div>
          {hoveredMarker?.data && <div style={{
        color: '#8c91a0',
        fontSize: '10px',
        marginTop: '4px'
      }} data-test="auto-InteractiveMap-div-016">
              {JSON.stringify(hoveredMarker.data).slice(0, 50)}
            </div>}
        </div>}
    </div>;
};
export default InteractiveMap;