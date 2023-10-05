# Edgegap Relay for Mirror
Documentation: https://docs.edgegap.com/docs/distributed-relay-manager/

## Prerequisites
- Unity project set up with the Mirror networking library installed
  - Supported Versions: [Mirror](https://assetstore.unity.com/packages/tools/network/mirror-129321) and [Mirror LTS](https://assetstore.unity.com/packages/tools/network/mirror-lts-102631)
- EdgegapTransport module downloaded and extracted

## Steps
1. Open your Unity project and navigate to the "Assets" folder.
2. Locate the "Mirror" folder within "Assets" and open it.
3. Within the "Mirror" folder, open the "Transports" folder.
4. Drag and drop the "Unity" folder from the extracted EdgegapTransport files into the "Transports" folder.
5. Open your NetworkManager script in the Unity Editor and navigate to the "Inspector" panel.
6. In the "Inspector" panel, locate the "Network Manager" component and click the "+" button next to the "Transport" property.
7. In the "Add Component" menu that appears, select "Edgegap Transport" to add it to the NetworkManager.
8. Drag the newly added "Edgegap Transport" component into the "Transport" property in the "Inspector" panel.

## Notes
- The EdgegapTransport module is only compatible with Mirror and Mirror LTS versions.
