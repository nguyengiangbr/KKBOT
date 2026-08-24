import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class KKBOTCommands {
    private context: vscode.ExtensionContext;
    private apiUrl: string;
    private pluginsPath: string;

    constructor(context: vscode.ExtensionContext) {
        this.context = context;
        this.apiUrl = vscode.workspace.getConfiguration('kkbot').get('apiUrl') || 'http://localhost:5000';
        this.pluginsPath = vscode.workspace.getConfiguration('kkbot').get('pluginsPath') || './plugins';
    }

    async createPlugin() {
        const pluginName = await vscode.window.showInputBox({
            prompt: 'Enter plugin name',
            placeHolder: 'MyGamePlugin',
            validateInput: (value) => {
                if (!value || value.trim() === '') {
                    return 'Plugin name is required';
                }
                if (!/^[a-zA-Z][a-zA-Z0-9]*$/.test(value)) {
                    return 'Plugin name must start with a letter and contain only alphanumeric characters';
                }
                return null;
            }
        });

        if (!pluginName) return;

        const pluginType = await vscode.window.showQuickPick([
            { label: 'Game Module', detail: 'Standard game automation module (IGameModule)' },
            { label: 'WASM Plugin', detail: 'High-performance WASM plugin (Rust)' },
            { label: 'Utility Plugin', detail: 'Helper plugin for common tasks' }
        ], { placeHolder: 'Select plugin type' });

        if (!pluginType) return;

        const templatePath = path.join(this.context.extensionPath, 'templates', pluginType.label.toLowerCase().replace(' ', '-'));
        const targetPath = path.join(this.pluginsPath, pluginName);

        if (fs.existsSync(targetPath)) {
            vscode.window.showErrorMessage(`Plugin directory ${targetPath} already exists`);
            return;
        }

        try {
            await this.copyTemplate(templatePath, targetPath, { pluginName });
            await this.updateCsproj(targetPath, pluginName);
            
            vscode.window.showInformationMessage(`Plugin ${pluginName} created at ${targetPath}`);
            
            const openFolder = await vscode.window.showInformationMessage(
                'Open plugin folder?', 'Yes', 'No'
            );
            if (openFolder === 'Yes') {
                await vscode.commands.executeCommand('vscode.openFolder', vscode.Uri.file(targetPath), true);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Failed to create plugin: ${error}`);
        }
    }

    async runScenario() {
        const scenarioFiles = await vscode.workspace.findFiles('**/*.kscn', '**/node_modules/**');
        if (scenarioFiles.length === 0) {
            vscode.window.showInformationMessage('No scenario files found. Create one first.');
            return;
        }

        const selected = await vscode.window.showQuickPick(
            scenarioFiles.map(f => ({ label: path.basename(f.fsPath), uri: f })),
            { placeHolder: 'Select scenario to run' }
        );

        if (!selected) return;

        const deviceId = await vscode.window.showInputBox({
            prompt: 'Enter device ID (optional)',
            placeHolder: 'emulator-5554'
        });

        const terminal = vscode.window.createTerminal({ name: `KKBOT: ${selected.label}` });
        terminal.show();
        
        const cmd = `kkbot scenario run ${selected.label} ${deviceId ? `--device ${deviceId}` : ''}`;
        terminal.sendText(cmd);
    }

    async deployPlugin() {
        const pluginFolders = await vscode.workspace.findFiles('**/plugins/**/*.csproj', '**/node_modules/**');
        if (pluginFolders.length === 0) {
            vscode.window.showInformationMessage('No plugin projects found.');
            return;
        }

        const selected = await vscode.window.showQuickPick(
            pluginFolders.map(f => ({ 
                label: path.basename(path.dirname(f.fsPath)), 
                path: path.dirname(f.fsPath) 
            })),
            { placeHolder: 'Select plugin to deploy' }
        );

        if (!selected) return;

        const terminal = vscode.window.createTerminal({ name: `KKBOT: Deploy ${selected.label}` });
        terminal.show();
        terminal.sendText(`cd ${selected.path} && dotnet publish -c Release -o ${this.pluginsPath}/${selected.label}`);
    }

    async openDashboard() {
        const panel = vscode.window.createWebviewPanel(
            'kkbotDashboard',
            'KKBOT Dashboard',
            vscode.ViewColumn.One,
            { enableScripts: true }
        );

        panel.webview.html = this.getDashboardHtml();
    }

    async buildPlugin() {
        const editor = vscode.window.activeTextEditor;
        if (!editor) {
            vscode.window.showErrorMessage('No active editor');
            return;
        }

        const csproj = await this.findCsproj(editor.document.uri);
        if (!csproj) {
            vscode.window.showErrorMessage('Not in a plugin project');
            return;
        }

        const terminal = vscode.window.createTerminal({ name: 'KKBOT: Build' });
        terminal.show();
        terminal.sendText(`dotnet build ${csproj} -c Release`);
    }

    async debugScenario() {
        const editor = vscode.window.activeTextEditor;
        if (!editor) return;

        const scenarioId = path.basename(editor.document.uri.fsPath, '.kscn');
        const config = {
            type: 'kkbot-scenario',
            request: 'launch',
            name: `Debug ${scenarioId}`,
            scenarioId,
            deviceId: vscode.workspace.getConfiguration('kkbot').get('debugPort')
        };

        await vscode.debug.startDebugging(this.context.workspaceState.get<vscode.WorkspaceFolder>('kkbot.workspaceFolder'), config);
    }

    async reloadPlugins() {
        vscode.commands.executeCommand('kkbot.plugins.refresh');
    }

    private async copyTemplate(src: string, dest: string, replacements: Record<string, string>) {
        // Copy template files and replace placeholders
        // Implementation would copy template files
    }

    private async updateCsproj(pluginPath: string, pluginName: string) {
        // Update .csproj with plugin name
    }

    private async findCsproj(uri: vscode.Uri): Promise<string | null> {
        let dir = path.dirname(uri.fsPath);
        while (dir !== path.parse(dir).root) {
            const files = fs.readdirSync(dir).filter(f => f.endsWith('.csproj'));
            if (files.length > 0) return path.join(dir, files[0]);
            dir = path.dirname(dir);
        }
        return null;
    }

    private getDashboardHtml(): string {
        return `<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>KKBOT Dashboard</title>
    <style>
        body { font-family: var(--vscode-font-family); padding: 20px; }
        .card { border: 1px solid var(--vscode-panel-border); border-radius: 4px; padding: 16px; margin: 8px 0; }
        .btn { background: var(--vscode-button-background); color: var(--vscode-button-foreground); 
             border: none; padding: 8px 16px; border-radius: 2px; cursor: pointer; margin: 4px; }
        .btn:hover { background: var(--vscode-button-hoverBackground); }
    </style>
</head>
<body>
    <h1>KKBOT Dashboard</h1>
    <div class="card">
        <h2>Quick Actions</h2>
        <button class="btn" onclick="runCommand('kkbot.runScenario')">Run Scenario</button>
        <button class="btn" onclick="runCommand('kkbot.createPlugin')">Create Plugin</button>
        <button class="btn" onclick="runCommand('kkbot.deployPlugin')">Deploy Plugin</button>
        <button class="btn" onclick="runCommand('kkbot.openDashboard')">Open Full Dashboard</button>
    </div>
    <script>
        function runCommand(cmd) {
            fetch('vscode://command/' + cmd);
        }
    </script>
</body>
</html>`;
    }
}