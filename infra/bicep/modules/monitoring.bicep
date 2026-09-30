param location string
param environmentName string

var nameSuffix = uniqueString(resourceGroup().id)

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${environmentName}-${nameSuffix}'
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${environmentName}-${nameSuffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

output applicationInsightsResourceId string = applicationInsights.id
output logAnalyticsWorkspaceResourceId string = logAnalyticsWorkspace.id
