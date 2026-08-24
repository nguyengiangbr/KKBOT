import * as vscode from 'vscode';
import { KKBOTCommands } from './commands';
import { KKBOTTreeProvider } from './treeProvider';

let commands: KKBOTCommands;
let treeProvider: KKBOTTreeProvider;

export function activate(context: vscode.ExtensionContext) {
    console.log('KKBOT Development Tools activated');

    commands = new KKBOTCommands(context);
    treeProvider = new KKBOTTreeProvider(context, commands);

    // Register tree views
    vscode.window.registerTreeDataProvider('kkbot.plugins', treeProvider.getPluginsProvider());
    vscode.window.registerTreeDataProvider('kkbot.scenarios', treeProvider.getScenariosProvider());
    vscode.window.registerTreeDataProvider('kkbot.instances', treeProvider.getInstancesProvider());
    vscode.window.registerTreeDataProvider('kkbot.devices', treeProvider.getDevicesProvider());

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('kkbot.createPlugin', () => commands.createPlugin()),
        vscode.commands.registerCommand('kkbot.runScenario', () => commands.runScenario()),
        vscode.commands.registerCommand('kkbot.deployPlugin', () => commands.deployPlugin()),
        vscode.commands.registerCommand('kkbot.openDashboard', () => commands.openDashboard()),
        vscode.commands.registerCommand('kkbot.buildPlugin', () => commands.buildPlugin()),
        vscode.commands.registerCommand('kkbot.debugScenario', () => commands.debugScenario())
    );

    // Set context for when clauses
    vscode.commands.executeCommand('setContext', 'kkbot.hasPlugins', true);
    vscode.commands.executeCommand('setContext', 'kkbot.hasScenarios', true);
    vscode.commands.executeCommand('setContext', 'kkbot.hasInstances', true);
    vscode.commands.executeCommand('setContext', 'kkbot.hasDevices', true);

    // Watch for plugin changes
    if (vscode.workspace.getConfiguration('kkbot').get('autoReload')) {
        const watcher = vscode.workspace.createFileSystemWatcher('**/plugins/**/*.cs');
        watcher.onDidChange(() => commands.reloadPlugins());
        watcher.onDidCreate(() => commands.reloadPlugins());
        watcher.onDidDelete(() => commands.reloadPlugins());
        context.subscriptions.push(watcher);
    }
}

export function deactivate() {
    console.log('KKBOT Development Tools deactivated');
}