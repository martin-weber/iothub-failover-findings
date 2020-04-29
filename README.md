# Azure IoT-Hub failover handling

In these articles we share our findings we gathered during a spike on investigating IoT Hub failover in one of our customer projects.
This is __not an official documentation__ and will not be regularly updated.  
Therefore, please consult first the official documentation, e.g. [IoT Hub high availability and disaster recovery](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr).

## Contents

* [Overview, Theory, Numbers](overview.md)
* [Failover handling on DeviceClients](failover-handling-on-deviceclients.md)
* [Monitoring IoT-Hub failover via API](monitoring-failover-via-api.md)
* [Idea to minimize impact of a failover](idea-to-minimize-impact.md)
* [Cross-region processing latencies](cross-region-latencies.md)


## Glossary

Term               | Description 
-------------------|-------------
DeviceClient       | The DeviceClient class of the Azure IoT Hub SDK is used on a device to connect to Azure IoT Hub
Consumer           | The application running in the cloud, which consumes and processes the telemetry events from the built-in Event Hub-compatible endpoint.


