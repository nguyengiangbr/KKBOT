export type RootStackParamList = {
  Auth: undefined;
  Main: undefined;
  Login: undefined;
  MainTabs: undefined;
  Dashboard: undefined;
  Instances: undefined;
  Plugins: undefined;
  Scenarios: undefined;
  Settings: undefined;
  InstanceDetail: { id: string };
  PluginDetail: { id: string };
  ScenarioDetail: { id: string };
  CreateInstance: undefined;
  CreateScenario: undefined;
};

export type TabParamList = {
  Dashboard: undefined;
  Instances: undefined;
  Plugins: undefined;
  Scenarios: undefined;
  Settings: undefined;
};

export type AuthStackParamList = {
  Login: undefined;
};

export type MainStackParamList = {
  MainTabs: undefined;
  InstanceDetail: { id: string };
  PluginDetail: { id: string };
  ScenarioDetail: { id: string };
};
