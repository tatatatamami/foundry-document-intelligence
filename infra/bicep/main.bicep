param location string

@minLength(1)
@maxLength(12)
param environmentName string

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    environmentName: environmentName
    location: location
  }
}

output applicationInsightsResourceId string = monitoring.outputs.applicationInsightsResourceId
output logAnalyticsWorkspaceResourceId string = monitoring.outputs.logAnalyticsWorkspaceResourceId
