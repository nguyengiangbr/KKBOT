import * as vscode from 'vscode';
import { KKBOTCommands } from './commands';

export interface KKBOTTreeItem extends vscode.TreeItem {
    id: string;
    type: 'plugin' | 'scenario' | 'instance' | 'device';
    data?: any;
}

export class KKBOTTreeProvider implements vscode.TreeDataProvider<KKBOTTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<KKBOTTreeItem | undefined | null | void> = new vscode.EventEmitter<KKBOTTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<KKBOTTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    private pluginsProvider: vscode.TreeDataProvider<KKBOTTreeItem>;
    private scenariosProvider: vscode.TreeDataProvider<KKBOTTreeItem>;
    private instancesProvider: vscode.TreeDataProvider<KKBOTTreeItem>;
    private devicesProvider: vscode.TreeDataProvider<KKBOTTreeItem>;

    constructor(
        private context: vscode.ExtensionContext,
        private commands: KKBOTCommands
    ) {
        this.pluginsProvider = new PluginsTreeProvider();
        this.scenariosProvider = new ScenariosTreeProvider();
        this.instancesProvider = new InstancesTreeProvider();
        this.devicesProvider = new DevicesTreeProvider();
    }

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getPluginsProvider(): vscode.TreeDataProvider<KKBOTTreeItem> {
        return this.pluginsProvider;
    }

    getScenariosProvider(): vscode.TreeDataProvider<KKBOTTreeItem> {
        return this.scenariosProvider;
    }

    getInstancesProvider(): vscode.TreeDataProvider<KKBOTTreeItem> {
        return this.instancesProvider;
    }

    getDevicesProvider(): vscode.TreeDataProvider<KKBOTTreeItem> {
        return this.devicesProvider;
    }

    getTreeItem(element: KKBOTTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: KKBOTTreeItem): Promise<KKBOTTreeItem[]> {
        if (!element) {
            return [
                { id: 'plugins', label: 'Plugins', type: 'plugin', collapsibleState: vscode.TreeItemCollapsibleState.Collapsed },
                { id: 'scenarios', label: 'Scenarios', type: 'scenario', collapsibleState: vscode.TreeItemCollapsibleState.Collapsed },
                { id: 'instances', label: 'Instances', type: 'instance', collapsibleState: vscode.TreeItemCollapsibleState.Collapsed },
                { id: 'devices', label: 'Devices', type: 'device', collapsibleState: vscode.TreeItemCollapsibleState.Collapsed }
            ];
        }
        return [];
    }
}

class PluginsTreeProvider implements vscode.TreeDataProvider<KKBOTTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<KKBOTTreeItem | undefined | null | void> = new vscode.EventEmitter<KKBOTTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<KKBOTTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: KKBOTTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: KKBOTTreeItem): Promise<KKBOTTreeItem[]> {
        if (!element) {
            // Return list of plugins
            return [
                { id: 'account-login', label: 'Account Login', type: 'plugin', collapsibleState: vscode.TreeItemCollapsibleState.None, command: { command: 'kkbot.openPlugin', title: 'Open', arguments: ['account-login'] } },
                { id: 'network-proxy', label: 'Network Proxy', type: 'plugin', collapsibleState: vscode.TreeItemCollapsibleState.None },
                { id: 'resource-optimizer', label: 'Resource Optimizer', type: 'plugin', collapsibleState: vscode.TreeItemCollapsibleState.None },
                { id: 'logistics-survival', label: 'Logistics & Survival', type: 'plugin', collapsibleState: vscode.TreeItemCollapsibleState.None },
                { id: 'combat-quest', label: 'Combat & Quest', type: 'plugin', collapsibleState: vscode.TreeItemCollapsibleState.None },
                { id: 'economy-trading', label: 'Economy & Trading', type: 'plugin', collapsibleState: vscode.TreeItemCollapsibleState.None },
                { id: 'anti-detection', label: 'Anti-Detection', type: 'plugin', collapsibleState: vscode.TreeItemCollapsibleState.None }
            ];
        }
        return [];
    }
}

class ScenariosTreeProvider implements vscode.TreeDataProvider<KKBOTTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<KKBOTTreeItem | undefined | null | void> = new vscode.EventEmitter<KKBOTTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<KKBOTTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: KKBOTTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: KKBOTTreeItem): Promise<KKBOTTreeItem[]> {
        if (!element) {
            return [
                { id: 'auto-login', label: 'Auto Login', type: 'scenario', collapsibleState: vscode.TreeItemCollapsibleState.None },
                { id: 'farm-loop', label: 'Farm Loop', type: 'scenario', collapsibleState: vscode.TreeItemCollapsibleState.None },
                { id: 'daily-quests', label: 'Daily Quests', type: 'scenario', collapsibleState: vscode.TreeItemCollapsibleState.None },
                { id: 'dungeon-run', label: 'Dungeon Run', type: 'scenario', collapsibleState: vscode.TreeItemCollapsibleState.None }
            ];
        }
        return [];
    }
}

class InstancesTreeProvider implements vscode.TreeDataProvider<KKBOTTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<KKBOTTreeItem | undefined | null | void> = new vscode.EventEmitter<KKBOTTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<KKBOTTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: KKBOTTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: KKBOTTreeItem): Promise<KKBOTTreeItem[]> {
        if (!element) {
            return [
                { id: 'instance-1', label: 'Account 1 (Running)', type: 'instance', collapsibleState: vscode.TreeItemCollapsibleState.None, iconPath: new vscode.ThemeIcon('play') },
                { id: 'instance-2', label: 'Account 2 (Idle)', type: 'instance', collapsibleState: vscode.TreeItemCollapsibleState.None, iconPath: new vscode.ThemeIcon('circle-outline') }
            ];
        }
        return [];
    }
}

class DevicesTreeProvider implements vscode.TreeDataProvider<KKBOTTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<KKBOTTreeItem | undefined | null | void> = new vscode.EventEmitter<KKBOTTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<KKBOTTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: KKBOTTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: KKBOTTreeItem): Promise<KKBOTTreeItem[]> {
        if (!element) {
            return [
                { id: 'emulator-5554', label: 'emulator-5554 (Connected)', type: 'device', collapsibleState: vscode.TreeItemCollapsibleState.None, iconPath: new vscode.ThemeIcon('device-mobile') },
                { id: 'device-123', label: 'device-123 (ADB)', type: 'device', collapsibleState: vscode.TreeItemCollapsibleState.None, iconPath: new vscode.ThemeIcon('device-mobile') }
            ];
        }
        return [];
    }
}