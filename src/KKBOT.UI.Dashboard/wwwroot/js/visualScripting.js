// Visual Scripting Canvas JavaScript Module
// Handles canvas rendering, node/edge interactions, and drag-and-drop

(function() {
    'use strict';

    const VisualScripting = {
        canvas: null,
        ctx: null,
        dotNetRef: null,
        workflow: null,
        nodes: new Map(),
        edges: new Map(),
        selectedNodeId: null,
        connectingFrom: null,
        panOffset: { x: 0, y: 0 },
        zoom: 1.0,
        isDragging: false,
        dragStartPos: { x: 0, y: 0 },
        nodeRadius: 8,
        portRadius: 6,
        gridSize: 20,
        showGrid: true,
        
        initializeCanvas(canvas, dotNetRef) {
            this.canvas = canvas;
            this.ctx = canvas.getContext('2d');
            this.dotNetRef = dotNetRef;
            
            this.resizeCanvas();
            this.setupEventListeners();
            this.startRenderLoop();
            
            window.addEventListener('resize', () => this.resizeCanvas());
        },
        
        resizeCanvas() {
            const rect = this.canvas.parentElement.getBoundingClientRect();
            this.canvas.width = rect.width * window.devicePixelRatio;
            this.canvas.height = rect.height * window.devicePixelRatio;
            this.ctx.scale(window.devicePixelRatio, window.devicePixelRatio);
            this.canvas.style.width = rect.width + 'px';
            this.canvas.style.height = rect.height + 'px';
        },
        
        setupEventListeners() {
            this.canvas.addEventListener('mousedown', (e) => this.onMouseDown(e));
            this.canvas.addEventListener('mousemove', (e) => this.onMouseMove(e));
            this.canvas.addEventListener('mouseup', (e) => this.onMouseUp(e));
            this.canvas.addEventListener('wheel', (e) => this.onWheel(e), { passive: false });
            this.canvas.addEventListener('contextmenu', (e) => e.preventDefault());
            
            // Touch support
            this.canvas.addEventListener('touchstart', (e) => this.onTouchStart(e), { passive: false });
            this.canvas.addEventListener('touchmove', (e) => this.onTouchMove(e), { passive: false });
            this.canvas.addEventListener('touchend', (e) => this.onTouchEnd(e));
        },
        
        getEventPos(e) {
            const rect = this.canvas.getBoundingClientRect();
            const clientX = e.clientX || (e.touches && e.touches[0].clientX) || 0;
            const clientY = e.clientY || (e.touches && e.touches[0].clientY) || 0;
            return {
                x: (clientX - this.canvas.getBoundingClientRect().left - this.panOffset.x) / this.zoom,
                y: (clientY - this.canvas.getBoundingClientRect().top - this.panOffset.y) / this.zoom
            };
        },
        
        onMouseDown(e) {
            if (e.button === 2) return; // Right click handled separately
            const pos = this.getEventPos(e);
            
            // Check if clicking on a port
            const portHit = this.hitTestPort(pos.x, pos.y);
            if (portHit) {
                this.handlePortClick(portHit.nodeId, portHit.portId, portHit.isInput);
                return;
            }
            
            // Check if clicking on a node
            const nodeHit = this.hitTestNode(pos.x, pos.y);
            if (nodeHit) {
                this.handleNodeClick(nodeHit);
                return;
            }
            
            // Canvas click - start panning
            this.isDragging = true;
            this.dragStartPos = { x: e.clientX, y: e.clientY };
            this.canvas.style.cursor = 'grabbing';
        },
        
        onMouseMove(e) {
            if (this.connectingFrom) {
                this.render(); // Update connection preview
                return;
            }
            
            if (this.isDragging) {
                const dx = e.clientX - this.dragStartPos.x;
                const dy = e.clientY - this.dragStartPos.y;
                this.panOffset.x += dx;
                this.panOffset.y += dy;
                this.dragStartPos = { x: e.clientX, y: e.clientY };
                this.render();
                return;
            }
            
            // Check hover for cursor
            const pos = this.getEventPos(e);
            const portHit = this.hitTestPort(pos.x, pos.y);
            if (portHit) {
                this.canvas.style.cursor = 'crosshair';
            } else if (this.hitTestNode(pos.x, pos.y)) {
                this.canvas.style.cursor = 'grab';
            } else {
                this.canvas.style.cursor = 'default';
            }
        },
        
        onMouseUp(e) {
            if (this.isDragging) {
                this.isDragging = false;
                this.canvas.style.cursor = 'default';
            }
        },
        
        onWheel(e) {
            e.preventDefault();
            const zoomFactor = e.deltaY > 0 ? 0.9 : 1.1;
            const newZoom = Math.max(0.1, Math.min(3.0, this.zoom * zoomFactor));
            
            // Zoom towards mouse position
            const pos = this.getEventPos(e);
            this.panOffset.x = pos.x - (pos.x - this.panOffset.x) * (newZoom / this.zoom);
            this.panOffset.y = pos.y - (pos.y - this.panOffset.y) * (newZoom / this.zoom);
            this.zoom = newZoom;
            this.render();
        },
        
        onTouchStart(e) {
            if (e.touches.length === 1) {
                const touch = e.touches[0];
                const mouseEvent = new MouseEvent('mousedown', {
                    clientX: touch.clientX,
                    clientY: touch.clientY,
                    button: 0
                });
                this.canvas.dispatchEvent(mouseEvent);
            } else if (e.touches.length === 2) {
                // Pinch zoom
                this.initialTouchDistance = this.getTouchDistance(e.touches[0], e.touches[1]);
                this.initialZoom = this.zoom;
            }
        },
        
        onTouchMove(e) {
            if (e.touches.length === 1) {
                const touch = e.touches[0];
                const mouseEvent = new MouseEvent('mousemove', {
                    clientX: touch.clientX,
                    clientY: touch.clientY
                });
                this.canvas.dispatchEvent(mouseEvent);
            } else if (e.touches.length === 2) {
                const distance = this.getTouchDistance(e.touches[0], e.touches[1]);
                this.zoom = Math.max(0.1, Math.min(3.0, this.initialZoom * (distance / this.initialTouchDistance)));
                this.render();
            }
        },
        
        onTouchEnd(e) {
            if (e.touches.length === 0) {
                const mouseEvent = new MouseEvent('mouseup', {});
                this.canvas.dispatchEvent(mouseEvent);
            }
        },
        
        getTouchDistance(t1, t2) {
            const dx = t1.clientX - t2.clientX;
            const dy = t1.clientY - t2.clientY;
            return Math.sqrt(dx * dx + dy * dy);
        },
        
        hitTestNode(x, y) {
            for (const [id, node] of this.nodes) {
                const nodeX = node.position.x;
                const nodeY = node.position.y;
                const width = 180;
                const height = 80 + (node.inputs.size + node.outputs.size) * 24;
                
                if (x >= nodeX && x <= nodeX + width && y >= nodeY && y <= nodeY + height) {
                    return node.id;
                }
            }
            return null;
        },
        
        hitTestPort(x, y) {
            for (const [id, node] of this.nodes) {
                // Check input ports
                let portY = node.position.y + 40;
                for (const [portId, port] of node.inputs) {
                    const portX = node.position.x;
                    const portY = node.position.y + 40 + Array.from(node.inputs.keys()).indexOf(portId) * 24;
                    
                    if (this.distance(x, y, portX, portY) < this.portRadius * 2) {
                        return { nodeId: node.id, portId, isInput: true };
                    }
                }
                
                // Check output ports
                for (const [portId, port] of node.outputs) {
                    const portX = node.position.x + 180;
                    const portY = node.position.y + 40 + Array.from(node.outputs.keys()).indexOf(portId) * 24;
                    
                    if (this.distance(x, y, portX, portY) < this.portRadius * 2) {
                        return { nodeId: node.id, portId, isInput: false };
                    }
                }
            }
            return null;
        },
        
        distance(x1, y1, x2, y2) {
            const dx = x1 - x2;
            const dy = y1 - y2;
            return Math.sqrt(dx * dx + dy * dy);
        },
        
        handleNodeClick(nodeId) {
            this.selectedNodeId = nodeId;
            this.dotNetRef.invokeMethodAsync('OnNodeClick', nodeId);
        },
        
        handlePortClick(nodeId, portId, isInput) {
            if (this.connectingFrom) {
                // Complete connection
                if (this.connectingFrom.isInput !== isInput) {
                    const sourceNodeId = this.connectingFrom.isInput ? nodeId : this.connectingFrom.nodeId;
                    const sourcePortId = this.connectingFrom.isInput ? portId : this.connectingFrom.portId;
                    const targetNodeId = isInput ? nodeId : this.connectingFrom.nodeId;
                    const targetPortId = isInput ? portId : this.connectingFrom.portId;
                    
                    this.dotNetRef.invokeMethodAsync('OnPortClick', targetNodeId, targetPortId, true);
                }
                this.connectingFrom = null;
            } else {
                // Start connection
                this.connectingFrom = { nodeId, portId, isInput };
            }
            this.render();
        },
        
        handleNodeClick(nodeId) {
            this.selectedNodeId = nodeId;
            this.connectingFrom = null;
            this.dotNetRef.invokeMethodAsync('OnNodeClick', nodeId);
        },
        
        renderWorkflow(workflowJson) {
            this.workflow = JSON.parse(workflowJson);
            this.nodes.clear();
            this.edges.clear();
            
            this.workflow.nodes.forEach(node => {
                this.nodes.set(node.id, {
                    ...node,
                    inputs: new Map(Object.entries(node.inputs)),
                    outputs: new Map(Object.entries(node.outputs)),
                    properties: new Map(Object.entries(node.properties))
                });
            });
            
            this.workflow.edges.forEach(edge => {
                this.edges.set(edge.id, edge);
            });
            
            this.render();
        },
        
        render() {
            const ctx = this.ctx;
            const canvas = this.canvas;
            
            // Clear
            ctx.save();
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.restore();
            
            // Apply transform
            ctx.save();
            ctx.translate(this.panOffset.x, this.panOffset.y);
            ctx.scale(this.zoom, this.zoom);
            
            // Draw grid
            if (this.showGrid) {
                this.drawGrid();
            }
            
            // Draw edges
            this.edges.forEach(edge => this.drawEdge(edge));
            
            // Draw connection preview
            if (this.connectingFrom) {
                this.drawConnectionPreview();
            }
            
            // Draw nodes
            this.nodes.forEach(node => this.drawNode(node));
            
            ctx.restore();
        },
        
        drawGrid() {
            const ctx = this.ctx;
            const gridSize = this.gridSize;
            const width = this.canvas.width / this.zoom;
            const height = this.canvas.height / this.zoom;
            
            ctx.strokeStyle = '#e0e0e0';
            ctx.lineWidth = 1 / this.zoom;
            
            ctx.beginPath();
            for (let x = 0; x <= width; x += gridSize) {
                ctx.moveTo(x, 0);
                ctx.lineTo(x, height);
            }
            for (let y = 0; y <= height; y += gridSize) {
                ctx.moveTo(0, y);
                ctx.lineTo(width, y);
            }
            ctx.stroke();
        },
        
        drawNode(node) {
            const ctx = this.ctx;
            const x = node.position.x;
            const y = node.position.y;
            const width = 180;
            const inputCount = node.inputs.size;
            const outputCount = node.outputs.size;
            const height = 80 + Math.max(inputCount, outputCount) * 24;
            const isSelected = this.selectedNodeId === node.id;
            
            // Node background
            ctx.fillStyle = isSelected ? '#e3f2fd' : '#ffffff';
            ctx.strokeStyle = isSelected ? '#1976d2' : '#e0e0e0';
            ctx.lineWidth = isSelected ? 2 / this.zoom : 1 / this.zoom;
            
            this.roundRect(ctx, x, y, 180, height, 8);
            ctx.fill();
            ctx.stroke();
            
            // Header
            ctx.fillStyle = node.color || '#1976d2';
            ctx.fillRect(x, y, 180, 36, 8, 8, 0, 0);
            
            // Title
            ctx.fillStyle = '#ffffff';
            ctx.font = 'bold 14px Roboto, sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText(node.name, x + 12, y + 24);
            
            // Ports
            const portYStart = y + 40;
            const portSpacing = 24;
            
            // Input ports
            let i = 0;
            for (const [portId, port] of node.inputs) {
                const py = portYStart + i * 24;
                this.drawPort(x, py, true, port, i);
                i++;
            }
            
            // Output ports
            i = 0;
            for (const [portId, port] of node.outputs) {
                const py = portYStart + i * 24;
                this.drawPort(x + 180, py, false, port, i);
                i++;
            }
            
            // Selection indicator
            if (this.selectedNodeId === node.id) {
                ctx.strokeStyle = '#1976d2';
                ctx.lineWidth = 2 / this.zoom;
                ctx.strokeRect(x - 2, y - 2, 184, height + 4);
            }
        },
        
        drawPort(x, y, isInput, port, index) {
            const ctx = this.ctx;
            const radius = this.portRadius;
            
            // Port circle
            ctx.beginPath();
            ctx.arc(x, y, radius, 0, Math.PI * 2);
            
            const color = this.getPortColor(port.dataType);
            ctx.fillStyle = color;
            ctx.fill();
            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 1 / this.zoom;
            ctx.stroke();
            
            // Port label
            ctx.fillStyle = '#333333';
            ctx.font = '12px Roboto, sans-serif';
            ctx.textAlign = isInput ? 'right' : 'left';
            ctx.textBaseline = 'middle';
            ctx.fillText(port.name, isInput ? x - 12 : x + 12, y);
            
            // Connection indicator
            if (port.connectedEdgeId) {
                ctx.fillStyle = '#4caf50';
                ctx.beginPath();
                ctx.arc(x, y, radius + 2, 0, Math.PI * 2);
                ctx.fill();
            }
        },
        
        getPortColor(dataType) {
            const colors = {
                'Any': '#9e9e9e',
                'Boolean': '#4caf50',
                'Integer': '#2196f3',
                'Number': '#2196f3',
                'String': '#ff9800',
                'Object': '#9c27b0',
                'Array': '#ff5722',
                'Frame': '#00bcd4',
                'Image': '#00bcd4',
                'Text': '#795548',
                'Json': '#607d8b',
                'Plugin': '#e91e63',
                'Instance': '#3f51b5',
                'Device': '#009688',
                'Model': '#673ab7',
                'Dataset': '#795548'
            };
            return colors[dataType] || colors['Any'];
        },
        
        drawEdge(edge) {
            const ctx = this.ctx;
            const sourceNode = this.nodes.get(edge.sourceNodeId);
            const targetNode = this.nodes.get(edge.targetNodeId);
            
            if (!sourceNode || !targetNode) return;
            
            const sourcePort = sourceNode.outputs.get(edge.sourcePortId);
            const targetPort = targetNode.inputs.get(edge.targetPortId);
            
            if (!sourcePort || !targetPort) return;
            
            const sx = sourceNode.position.x + 180;
            const sy = sourceNode.position.y + 40 + Array.from(sourceNode.outputs.keys()).indexOf(edge.sourcePortId) * 24;
            const tx = targetNode.position.x;
            const ty = targetNode.position.y + 40 + Array.from(targetNode.inputs.keys()).indexOf(edge.targetPortId) * 24;
            
            ctx.strokeStyle = edge.style?.color || '#1976d2';
            ctx.lineWidth = (edge.style?.width || 2) / this.zoom;
            ctx.setLineDash(edge.style?.lineStyle === 'dashed' ? [5 / this.zoom, 5 / this.zoom] : 
                           edge.style?.lineStyle === 'dotted' ? [2 / this.zoom, 4 / this.zoom] : []);
            
            ctx.beginPath();
            ctx.moveTo(sx, sy);
            
            // Bezier curve
            const cp1x = sx + 80;
            const cp1y = sy;
            const cp2x = tx - 80;
            const cp2y = ty;
            ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, tx, ty);
            ctx.stroke();
            
            // Arrow head
            const angle = Math.atan2(ty - cp2y, tx - cp2x);
            const arrowSize = 8 / this.zoom;
            ctx.beginPath();
            ctx.moveTo(tx, ty);
            ctx.lineTo(tx - arrowSize * Math.cos(angle - Math.PI / 6), ty - arrowSize * Math.sin(angle - Math.PI / 6));
            ctx.lineTo(tx - arrowSize * Math.cos(angle + Math.PI / 6), ty - arrowSize * Math.sin(angle + Math.PI / 6));
            ctx.closePath();
            ctx.fillStyle = edge.style?.color || '#1976d2';
            ctx.fill();
            
            ctx.setLineDash([]);
            
            // Animated flow
            if (edge.style?.animated) {
                // Draw moving dots along the path
                const time = Date.now() / 1000;
                const progress = (time * 0.5) % 1;
                const px = this.getBezierPoint(sx, sy, cp1x, cp1y, cp2x, cp2y, tx, ty, progress);
                ctx.fillStyle = '#ffffff';
                ctx.beginPath();
                ctx.arc(px.x, px.y, 3 / this.zoom, 0, Math.PI * 2);
                ctx.fill();
            }
        },
        
        getBezierPoint(x1, y1, cp1x, cp1y, cp2x, cp2y, x2, y2, t) {
            const u = 1 - t;
            const uu = u * u;
            const uuu = uu * u;
            const tt = t * t;
            const ttt = tt * t;
            
            const x = uuu * x1 + 3 * uu * t * cp1x + 3 * u * tt * cp2x + ttt * x2;
            const y = uuu * y1 + 3 * uu * t * cp1y + 3 * u * tt * cp2y + ttt * y2;
            
            return { x, y };
        },
        
        drawConnectionPreview() {
            if (!this.connectingFrom) return;
            
            const ctx = this.ctx;
            const sourceNode = this.nodes.get(this.connectingFrom.nodeId);
            if (!sourceNode) return;
            
            const sourcePort = this.connectingFrom.isInput 
                ? sourceNode.inputs.get(this.connectingFrom.portId)
                : sourceNode.outputs.get(this.connectingFrom.portId);
            
            if (!sourcePort) return;
            
            const sx = this.connectingFrom.isInput ? sourceNode.position.x : sourceNode.position.x + 180;
            const sy = sourceNode.position.y + 40 + 
                (this.connectingFrom.isInput 
                    ? Array.from(sourceNode.inputs.keys()).indexOf(this.connectingFrom.portId)
                    : Array.from(sourceNode.outputs.keys()).indexOf(this.connectingFrom.portId)) * 24;
            
            // Get mouse position in canvas coordinates
            const mx = this.mouseX;
            const my = this.mouseY;
            
            ctx.strokeStyle = '#1976d2';
            ctx.lineWidth = 2 / this.zoom;
            ctx.setLineDash([5 / this.zoom, 5 / this.zoom]);
            
            ctx.beginPath();
            ctx.moveTo(sx, sy);
            ctx.quadraticCurveTo((sx + mx) / 2, sy, mx, my);
            ctx.stroke();
            ctx.setLineDash([]);
        },
        
        startRenderLoop() {
            const render = () => {
                this.render();
                requestAnimationFrame(() => this.renderLoop());
            };
            this.renderLoop = render;
            render();
        },
        
        dispose() {
            this.canvas = null;
            this.ctx = null;
            this.dotNetRef = null;
            this.nodes.clear();
            this.edges.clear();
        }
    };
    
    // Export to global scope
    window.visualScripting = VisualScripting;
})();