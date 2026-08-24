// Visual Scenario Editor - Canvas Rendering & Interactions
// This file handles all canvas drawing, drag-drop, pan/zoom, and node/edge interactions

let canvas = null;
let ctx = null;
let minimapCanvas = null;
let minimapCtx = null;
let canvasRect = null;
let dotnetRef = null;

let nodes = [];
let edges = [];
let zoom = 1.0;
let panX = 0;
let panY = 0;
let selectedNodeId = null;
let draggedNode = null;
let dragStart = { x: 0, y: 0 };
let isPanning = false;
let panStart = { x: 0, y: 0 };
let connectionStart = null; // { nodeId, portId, portType, x, y }

const NODE_WIDTH = 180;
const NODE_HEIGHT = 80;
const PORT_RADIUS = 8;
const PORT_GAP = 12;

window.initCanvas = async function (canvasElement, minimapElement, initialZoom, initialPanX, initialPanY) {
    canvas = canvasElement;
    ctx = canvas.getContext('2d');
    minimapCanvas = minimapElement;
    minimapCtx = minimapCanvas.getContext('2d');
    zoom = initialZoom;
    panX = initialPanX;
    panY = initialPanY;
    canvasRect = canvas.getBoundingClientRect();
    
    // Set canvas size
    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);
    
    // Event listeners
    canvas.addEventListener('mousedown', onMouseDown);
    canvas.addEventListener('mousemove', onMouseMove);
    canvas.addEventListener('mouseup', onMouseUp);
    canvas.addEventListener('wheel', onWheel, { passive: false });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    
    // Start render loop
    requestAnimationFrame(renderLoop);
};

function resizeCanvas() {
    if (!canvas) return;
    canvasRect = canvas.getBoundingClientRect();
    canvas.width = canvasRect.width * window.devicePixelRatio;
    canvas.height = canvasRect.height * window.devicePixelRatio;
    canvas.style.width = canvasRect.width + 'px';
    canvas.style.height = canvasRect.height + 'px';
    ctx.scale(window.devicePixelRatio, window.devicePixelRatio);
    
    if (minimapCanvas) {
        minimapCanvas.width = 200 * window.devicePixelRatio;
        minimapCanvas.height = 150 * window.devicePixelRatio;
        minimapCanvas.style.width = '200px';
        minimapCanvas.style.height = '150px';
        minimapCtx.scale(window.devicePixelRatio, window.devicePixelRatio);
    }
}

window.renderCanvas = async function (canvasElement, canvasData) {
    if (!canvas) return;
    nodes = canvasData.Nodes || [];
    edges = canvasData.Edges || [];
    zoom = canvasData.Zoom || 1.0;
    panX = canvasData.PanX || 0;
    panY = canvasData.PanY || 0;
    selectedNodeId = canvasData.SelectedNodeId || null;
    
    await drawCanvas();
    await drawMinimap();
}

function drawCanvas() {
    if (!ctx || !canvas) return;
    
    const width = canvasRect.width;
    const height = canvasRect.height;
    
    // Clear
    ctx.clearRect(0, 0, width, height);
    
    // Grid background
    drawGrid();
    
    // Transform for pan/zoom
    ctx.save();
    ctx.translate(panX, panY);
    ctx.scale(zoom, zoom);
    
    // Draw edges first (behind nodes)
    drawEdges();
    
    // Draw nodes
    drawNodes();
    
    // Draw connection preview
    if (connectionStart) {
        drawConnectionPreview();
    }
    
    ctx.restore();
    
    // Draw selection indicator (in screen space)
    if (selectedNodeId) {
        drawSelectionIndicator();
    }
}

function drawGrid() {
    const gridSize = 20 * zoom;
    const offsetX = panX % gridSize;
    const offsetY = panY % gridSize;
    
    ctx.strokeStyle = 'rgba(0,0,0,0.05)';
    ctx.lineWidth = 1 / zoom;
    
    ctx.beginPath();
    for (let x = offsetX; x < canvasRect.width; x += gridSize) {
        ctx.moveTo(x, 0);
        ctx.lineTo(x, canvasRect.height);
    }
    for (let y = offsetY; y < canvasRect.height; y += gridSize) {
        ctx.moveTo(0, y);
        ctx.lineTo(canvasRect.width, y);
    }
    ctx.stroke();
}

function drawNodes() {
    nodes.forEach(node => {
        const x = node.X;
        const y = node.Y;
        const w = NODE_WIDTH;
        const h = NODE_HEIGHT;
        const isSelected = node.Id === selectedNodeId;
        
        // Node background
        ctx.fillStyle = isSelected ? '#e3f2fd' : '#ffffff';
        ctx.strokeStyle = isSelected ? '#1976d2' : '#ddd';
        ctx.lineWidth = isSelected ? 2 / zoom : 1 / zoom;
        ctx.shadowColor = 'rgba(0,0,0,0.1)';
        ctx.shadowBlur = 4 / zoom;
        ctx.shadowOffsetX = 1 / zoom;
        ctx.shadowOffsetY = 2 / zoom;
        
        roundRect(ctx, x, y, w, h, 8);
        ctx.fill();
        ctx.stroke();
        
        // Node header
        ctx.fillStyle = isSelected ? '#1976d2' : '#424242';
        ctx.font = 'bold 12px Inter, system-ui';
        ctx.textAlign = 'left';
        ctx.fillText(node.DisplayName, x + 12, y + 22);
        
        // Node type badge
        ctx.fillStyle = 'rgba(0,0,0,0.08)';
        ctx.font = '10px Inter, system-ui';
        ctx.fillText(node.Type, x + 12, y + 40);
        
        // Ports
        drawPorts(node);
        
        // Reset shadow
        ctx.shadowColor = 'transparent';
        ctx.shadowBlur = 0;
        ctx.shadowOffsetX = 0;
        ctx.shadowOffsetY = 0;
    });
}

function drawPorts(node) {
    const x = node.X;
    const y = node.Y;
    const w = NODE_WIDTH;
    const h = NODE_HEIGHT;
    
    const inputPorts = node.Ports.filter(p => p.Type === 'Input');
    const outputPorts = node.Ports.filter(p => p.Type === 'Output');
    
    // Input ports (left side)
    inputPorts.forEach((port, index) => {
        const portY = y + h / 2 - (inputPorts.length - 1) * PORT_GAP / 2 + index * PORT_GAP;
        drawPort(x, portY, port.Id, true, node.Id);
    });
    
    // Output ports (right side)
    outputPorts.forEach((port, index) => {
        const portY = y + h / 2 - (outputPorts.length - 1) * PORT_GAP / 2 + index * PORT_GAP;
        drawPort(x + w, portY, port.Id, false, node.Id);
    });
}

function drawPort(x, y, portId, isInput, nodeId) {
    const radius = PORT_RADIUS;
    const isConnected = edges.some(e => 
        (isInput && e.TargetNodeId === nodeId && e.TargetPort === portId) ||
        (!isInput && e.SourceNodeId === nodeId && e.SourcePort === portId)
    );
    
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, Math.PI * 2);
    
    if (isConnected) {
        ctx.fillStyle = '#4caf50';
    } else if (isHoveringPort(nodeId, portId)) {
        ctx.fillStyle = '#ff9800';
    } else {
        ctx.fillStyle = isInput ? '#2196f3' : '#f44336';
    }
    
    ctx.fill();
    ctx.strokeStyle = '#fff';
    ctx.lineWidth = 1.5 / zoom;
    ctx.stroke();
    
    // Port label
    const portName = node.Ports.find(p => p.Id === portId)?.Name || '';
    ctx.fillStyle = '#333';
    ctx.font = '10px Inter, system-ui';
    ctx.textAlign = isInput ? 'right' : 'left';
    ctx.fillText(portName, isInput ? x - radius - 4 : x + radius + 4, y + 3);
}

function drawEdges() {
    ctx.lineWidth = 2 / zoom;
    
    edges.forEach(edge => {
        const sourceNode = nodes.find(n => n.Id === edge.SourceNodeId);
        const targetNode = nodes.find(n => n.Id === edge.TargetNodeId);
        if (!sourceNode || !targetNode) return;
        
        const sourcePort = sourceNode.Ports.find(p => p.Id === edge.SourcePort);
        const targetPort = targetNode.Ports.find(p => p.Id === edge.TargetPort);
        if (!sourcePort || !targetPort) return;
        
        const sourceX = sourceNode.X + (sourcePort.Type === 'Output' ? NODE_WIDTH : 0);
        const sourceY = sourceNode.Y + NODE_HEIGHT / 2 - 
            (sourceNode.Ports.filter(p => p.Type === sourcePort.Type).length - 1) * PORT_GAP / 2 +
            sourceNode.Ports.filter(p => p.Type === sourcePort.Type).findIndex(p => p.Id === sourcePort.Id) * PORT_GAP;
        
        const targetX = targetNode.X + (targetPort.Type === 'Input' ? 0 : NODE_WIDTH);
        const targetY = targetNode.Y + NODE_HEIGHT / 2 - 
            (targetNode.Ports.filter(p => p.Type === targetPort.Type).length - 1) * PORT_GAP / 2 +
            targetNode.Ports.filter(p => p.Type === targetPort.Type).findIndex(p => p.Id === targetPort.Id) * PORT_GAP;
        
        drawBezierCurve(sourceX, sourceY, targetX, targetY);
    });
}

function drawBezierCurve(x1, y1, x2, y2) {
    const dx = x2 - x1;
    const dy = y2 - y1;
    const distance = Math.sqrt(dx * dx + dy * dy);
    const curveStrength = Math.min(distance * 0.3, 100);
    
    const cp1x = x1 + curveStrength;
    const cp1y = y1;
    const cp2x = x2 - curveStrength;
    const cp2y = y2;
    
    ctx.beginPath();
    ctx.moveTo(x1, y1);
    ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, x2, y2);
    ctx.strokeStyle = '#757575';
    ctx.stroke();
    
    // Arrow head
    drawArrowHead(x2, y2, cp2x, cp2y);
}

function drawArrowHead(x, y, cp2x, cp2y) {
    const angle = Math.atan2(y - cp2y, x - cp2x);
    const headLength = 10 / zoom;
    const headAngle = Math.PI / 6;
    
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(x - headLength * Math.cos(angle - headAngle), y - headLength * Math.sin(angle - headAngle));
    ctx.lineTo(x - headLength * Math.cos(angle + headAngle), y - headLength * Math.sin(angle + headAngle));
    ctx.closePath();
    ctx.fillStyle = '#757575';
    ctx.fill();
}

function drawConnectionPreview() {
    if (!connectionStart) return;
    
    const mousePos = getMousePos();
    if (!mousePos) return;
    
    const startNode = nodes.find(n => n.Id === connectionStart.nodeId);
    if (!startNode) return;
    
    const startPort = sourceNode.Ports.find(p => p.Id === connectionStart.portId);
    if (!startPort) return;
    
    const startX = startNode.X + (startPort.Type === 'Output' ? NODE_WIDTH : 0);
    const startY = startNode.Y + NODE_HEIGHT / 2;
    
    const endX = mousePos.x;
    const endY = mousePos.y;
    
    ctx.beginPath();
    ctx.moveTo(startX, startY);
    ctx.lineTo(endX, endY);
    ctx.strokeStyle = '#ff9800';
    ctx.lineWidth = 2 / zoom;
    ctx.setLineDash([5 / zoom, 5 / zoom]);
    ctx.stroke();
    ctx.setLineDash([]);
}

function drawSelectionIndicator() {
    const node = nodes.find(n => n.Id === selectedNodeId);
    if (!node) return;
    
    const x = node.X + panX * zoom;
    const y = node.Y + panY * zoom;
    const w = NODE_WIDTH * zoom;
    const h = NODE_HEIGHT * zoom;
    
    ctx.strokeStyle = '#1976d2';
    ctx.lineWidth = 2;
    ctx.setLineDash([5, 5]);
    ctx.strokeRect(x - 4, y - 4, w + 8, h + 8);
    ctx.setLineDash([]);
}

function drawMinimap() {
    if (!minimapCtx || !minimapCanvas) return;
    
    const width = 200;
    const height = 150;
    const scaleX = width / canvasRect.width;
    const scaleY = height / canvasRect.height;
    const scale = Math.min(scaleX, scaleY) * 0.9;
    
    minimapCtx.clearRect(0, 0, width, height);
    
    // Background
    minimapCtx.fillStyle = '#f5f5f5';
    minimapCtx.fillRect(0, 0, width, height);
    
    // Viewport rectangle
    const viewportX = -panX * scale + (width - canvasRect.width * scale) / 2;
    const viewportY = -panY * scale + (height - canvasRect.height * scale) / 2;
    const viewportW = canvasRect.width * scale;
    const viewportH = canvasRect.height * scale;
    
    minimapCtx.strokeStyle = '#1976d2';
    minimapCtx.lineWidth = 2;
    minimapCtx.strokeRect(viewportX, viewportY, viewportW, viewportH);
    
    // Nodes
    nodes.forEach(node => {
        const x = (node.X + panX) * scale + (width - canvasRect.width * scale) / 2;
        const y = (node.Y + panY) * scale + (height - canvasRect.height * scale) / 2;
        const w = NODE_WIDTH * scale;
        const h = NODE_HEIGHT * scale;
        
        minimapCtx.fillStyle = node.Id === selectedNodeId ? '#1976d2' : '#999';
        minimapCtx.fillRect(x, y, w, h);
    });
}

function drawNodes() {
    nodes.forEach(node => {
        const x = node.X;
        const y = node.Y;
        const w = NODE_WIDTH;
        const h = NODE_HEIGHT;
        const isSelected = node.Id === selectedNodeId;
        
        // Node background
        ctx.fillStyle = isSelected ? '#e3f2fd' : '#ffffff';
        ctx.strokeStyle = isSelected ? '#1976d2' : '#ddd';
        ctx.lineWidth = isSelected ? 2 / zoom : 1 / zoom;
        ctx.shadowColor = 'rgba(0,0,0,0.1)';
        ctx.shadowBlur = 4 / zoom;
        ctx.shadowOffsetX = 1 / zoom;
        ctx.shadowOffsetY = 2 / zoom;
        
        roundRect(ctx, x, y, w, h, 8);
        ctx.fill();
        ctx.stroke();
        
        // Node header
        ctx.fillStyle = isSelected ? '#1976d2' : '#424242';
        ctx.font = 'bold 12px Inter, system-ui';
        ctx.textAlign = 'left';
        ctx.fillText(node.DisplayName, x + 12, y + 22);
        
        // Node type badge
        ctx.fillStyle = 'rgba(0,0,0,0.08)';
        ctx.font = '10px Inter, system-ui';
        ctx.fillText(node.Type, x + 12, y + 40);
        
        // Ports
        drawPorts(node);
        
        // Reset shadow
        ctx.shadowColor = 'transparent';
        ctx.shadowBlur = 0;
        ctx.shadowOffsetX = 0;
        ctx.shadowOffsetY = 0;
    });
}

// Helper functions
function roundRect(ctx, x, y, width, height, radius) {
    ctx.beginPath();
    ctx.moveTo(x + radius, y);
    ctx.lineTo(x + width - radius, y);
    ctx.quadraticCurveTo(x + width, y, x + width, y + radius);
    ctx.lineTo(x + width, y + height - radius);
    ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
    ctx.lineTo(x + radius, y + height);
    ctx.quadraticCurveTo(x, y + height, x, y + height - radius);
    ctx.lineTo(x, y + radius);
    ctx.quadraticCurveTo(x, y, x + radius, y);
    ctx.closePath();
}

function getMousePos() {
    // This would need to be passed from mouse event
    return null;
}

function isHoveringPort(nodeId, portId) {
    // Implementation would check mouse position
    return false;
}

window.getCanvasRect = function () {
    if (!canvas) return { left: 0, top: 0 };
    const rect = canvas.getBoundingClientRect();
    return { left: rect.left, top: rect.top, width: rect.width, height: rect.height };
};

// Event handlers
function onMouseDown(e) {
    const rect = canvas.getBoundingClientRect();
    const x = (e.clientX - rect.left - panX) / zoom;
    const y = (e.clientY - rect.top - panY) / zoom;
    
    // Check if clicking on a port
    const portHit = hitTestPort(x, y);
    if (portHit) {
        connectionStart = {
            nodeId: portHit.nodeId,
            portId: portHit.portId,
            portType: portHit.portType,
            x: e.clientX,
            y: e.clientY
        };
        return;
    }
    
    // Check if clicking on a node
    const nodeHit = hitTestNode(x, y);
    if (nodeHit) {
        if (e.button === 0) { // Left click
            if (e.shiftKey) {
                // Multi-select
            } else {
                selectNode(nodeHit.Id);
            }
            draggedNode = { nodeId: nodeHit.Id, startX: x, startY: y };
            dragStart = { x: e.clientX, y: e.clientY };
        } else if (e.button === 2) { // Right click
            selectNode(nodeHit.Id);
            showContextMenu(e.clientX, e.clientY, nodeHit.Id);
        }
        return;
    }
    
    // Click on canvas background
    if (e.button === 0) {
        // Start panning with middle mouse or shift+left
        if (e.button === 1 || (e.button === 0 && e.shiftKey)) {
            isPanning = true;
            panStart = { x: e.clientX, y: e.clientY };
            canvas.style.cursor = 'grabbing';
        } else {
            selectNode(null);
        }
    } else if (e.button === 1) { // Middle mouse
        isPanning = true;
        panStart = { x: e.clientX, y: e.clientY };
        canvas.style.cursor = 'grabbing';
    }
}

function onMouseMove(e) {
    const rect = canvas.getBoundingClientRect();
    const x = (e.clientX - rect.left - panX) / zoom;
    const y = (e.clientY - rect.top - panY) / zoom;
    
    if (connectionStart) {
        // Update connection preview - trigger re-render
        requestAnimationFrame(() => drawCanvas());
        return;
    }
    
    if (draggedNode) {
        const dx = (e.clientX - dragStart.x) / zoom;
        const dy = (e.clientY - dragStart.y) / zoom;
        
        const node = nodes.find(n => n.Id === draggedNode.nodeId);
        if (node) {
            node.X = draggedNode.startX + dx;
            node.Y = draggedNode.startY + dy;
            requestAnimationFrame(() => drawCanvas());
        }
        return;
    }
    
    if (isPanning) {
        const dx = (e.clientX - panStart.x) / zoom;
        const dy = (e.clientY - panStart.y) / zoom;
        panX += dx;
        panY += dy;
        panStart = { x: e.clientX, y: e.clientY };
        drawCanvas();
        return;
    }
    
    // Update hover states
    // requestAnimationFrame(() => drawCanvas());
}

function onMouseUp(e) {
    if (connectionStart) {
        // Try to complete connection
        const x = (e.clientX - canvas.getBoundingClientRect().left - panX) / zoom;
        const y = (e.clientY - canvas.getBoundingClientRect().top - panY) / zoom;
        
        const targetPort = hitTestPort(
            (e.clientX - canvas.getBoundingClientRect().left - panX) / zoom,
            (e.clientY - canvas.getBoundingClientRect().top - panY) / zoom
        );
        
        if (targetPort && targetPort.nodeId !== connectionStart.nodeId && 
            targetPort.portType !== connectionStart.portType) {
            // Valid connection!
            if (connectionStart.portType === 'Output' && targetPort.portType === 'Input') {
                dotnetRef.invokeMethodAsync('OnEdgeCreated', 
                    connectionStart.nodeId, connectionStart.portId,
                    targetPort.nodeId, targetPort.portId);
            } else if (connectionStart.portType === 'Input' && targetPort.portType === 'Output') {
                dotnetRef.invokeMethodAsync('OnEdgeCreated',
                    targetPort.nodeId, targetPort.portId,
                    connectionStart.nodeId, connectionStart.portId);
            }
        }
        connectionStart = null;
        drawCanvas();
        return;
    }
    
    if (draggedNode) {
        draggedNode = null;
        canvas.style.cursor = 'default';
        return;
    }
    
    if (isPanning) {
        isPanning = false;
        canvas.style.cursor = 'default';
        return;
    }
}

function onWheel(e) {
    e.preventDefault();
    const delta = e.deltaY > 0 ? 0.9 : 1.1;
    const newZoom = Math.max(0.25, Math.min(3.0, zoom * delta));
    
    // Zoom towards mouse position
    const rect = canvas.getBoundingClientRect();
    const mouseX = (e.clientX - rect.left - panX) / zoom;
    const mouseY = (e.clientY - rect.top - panY) / zoom;
    
    panX = e.clientX - rect.left - mouseX * newZoom;
    panY = e.clientY - rect.top - mouseY * newZoom;
    zoom = newZoom;
    
    dotnetRef.invokeMethodAsync('OnZoomChanged', zoom, panX, panY);
    drawCanvas();
    drawMinimap();
}

function hitTestNode(x, y) {
    for (const node of nodes) {
        if (x >= node.X && x <= node.X + NODE_WIDTH &&
            y >= node.Y && y <= node.Y + NODE_HEIGHT) {
            return node;
        }
    }
    return null;
}

function hitTestPort(x, y) {
    for (const node of nodes) {
        for (const port of node.Ports) {
            const portX = port.Type === 'Input' ? node.X : node.X + NODE_WIDTH;
            const portIndex = node.Ports.filter(p => p.Type === port.Type).findIndex(p => p.Id === port.Id);
            const portY = node.Y + NODE_HEIGHT / 2 - 
                (node.Ports.filter(p => p.Type === port.Type).length - 1) * PORT_GAP / 2 +
                portIndex * PORT_GAP;
            
            const dx = x - portX;
            const dy = y - portY;
            const dist = Math.sqrt(dx * dx + dy * dy);
            
            if (dist <= PORT_RADIUS * 2) {
                return { nodeId: node.Id, portId: port.Id, portType: port.Type };
            }
        }
    }
    return null;
}

function selectNode(nodeId) {
    selectedNodeId = nodeId;
    if (dotnetRef) {
        dotnetRef.invokeMethodAsync('OnNodeSelected', nodeId);
    }
    drawCanvas();
}

function showContextMenu(x, y, nodeId) {
    if (dotnetRef) {
        dotnetRef.invokeMethodAsync('OnContextMenu', x, y, nodeId);
    }
}

let renderFrameId = null;

function renderLoop() {
    drawCanvas();
    drawMinimap();
    renderFrameId = requestAnimationFrame(renderLoop);
}

window.setupCanvasInteractions = function (canvasElement, dotnetReference) {
    dotnetRef = dotnetReference;
    initCanvas(canvasElement, document.getElementById('minimapCanvas'), 1.0, 0, 0);
};

// Utility functions for C# interop
window.showContextMenu = function (x, y, nodeId) {
    showContextMenu(x, y, nodeId);
};

window.zoomIn = function () {
    zoom = Math.min(zoom * 1.2, 3.0);
    drawCanvas();
    drawMinimap();
};

window.zoomOut = function () {
    zoom = Math.max(zoom / 1.2, 0.25);
    drawCanvas();
    drawMinimap();
};

window.resetZoom = function () {
    zoom = 1.0;
    panX = 0;
    panY = 0;
    drawCanvas();
    drawMinimap();
};

window.prompt = function (message, defaultValue) {
    return new Promise(resolve => {
        const result = window.prompt(message, defaultValue);
        resolve(result);
    });
};