# Theory and Context
Azure IoT-Hub supports _Intra-region HA (High Availability)_ and _Cross Region DR (Disaster Recovery)_ which cover different cases as described in the related docs [IoT Hub high availability and disaster recovery](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr). In this article we will focus on the later, but let's have a look at the different solutions first:

[__Intra-region HA__](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr#intra-region-ha) describes the solution provided by redundancies of the components of the service. According to the documentation for Intra-region HA (as of March 30th, 2020): _"The [SLA published by the IoT Hub service](https://azure.microsoft.com/en-us/support/legal/sla/iot-hub/v1_2/) is achived by making use of these redundancies_".  
The documentation recommends: _"Appropriate [retry policies](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-reliability-features-in-sdks) must be built in to the components interacting with a cloud application to deal with transient failures"_.  
These topics and appropriate solutions are common best practices and already covered by a lot of other articles and the official documentation.  

[__Cross-region DR__](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr#cross-region-dr) is useful for business continuity in the rare cases when a data center experiences outages due to power failures or other regional failures that can not be fully handled by intra-region HA.  
This solution works by initiate a manual failover by the customer or a Microsoft initiated failover form one to the other [Azure go-paired region](https://docs.microsoft.com/en-us/azure/best-practices-availability-paired-regions) and later back.

Here are some related parts in the docs to consider, when implementing for cross-region DR:
* [Cross region DR: Especially the table and the Caution-box](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr#cross-region-dr)
* [Microsoft-initiated failover](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr#microsoft-initiated-failover) and [Manual failover](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr#manual-failover)
* [Choose the right HA/DR option](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr#choose-the-right-hadr-option)

# Timeline of a manual IoT-Hub failover
When a manual failover is initiated on an IoT Hub, the following events can be observed:

1. The Status of the IoT-Hub changes from "Active" to "FailingOver"
2. IoT Hub stops forwarding messages
   * The connected DeviceClients get exceptions when trying to send messages.  
   * The Consumers stop getting messages as they are no longer forwarded.  
   * But there is no Error on the consumers Event Hub Connections.
3. The connection string of the IoT-Hubs built-in Event Hub-compatible endpoint changes.  
   * Therefore, the Consumers need to reconnect after this happened.
4. The Status of the IoT-Hub changes from "FailingOver" back to "Active".
5. The old built-in Event Hub-compatible endpoint closes the connections or gets errors.

![Failover timeline](assets/failover-timeline.png "Failover timeline (Example in minutes)")

## Investigation on failover time

The documentation on [IoT Hub HA and DR - Manual failover section](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-ha-dr#manual-failover), regarding recovery time objective indicate that:

    The RTO is currently a function of the number of devices registered against the IoT hub instance being failed over.
    
    You can expect the RTO for a hub hosting approximately 100,000 devices to be in the ballpark of 15 minutes

Using the application described in the "[Detect failover by observing the IoT-Hub](#detect-failover-by-observing-the-IoT-Hub)" section as a basis, it is possible to check the recovery time objective in the chosen configuration.

The following tests were executed on a IoT Hub S3, single unit in East US:

| Device number | Twin Size | Failover | Time (seconds)
| :-------------: | :-------------: | :-------------: | :-------------: |
| 80k           | Empty twin        | East US -> West US | 518
| 80k           | Empty twin        | West US -> East US | 543
| 120k          | Empty twin        | East US -> West US | 539
| 120k          | Empty twin        | West US -> East US | 565
| 120k          | 4kb 0-filled twin | East US -> West US | 548
| 300k          | Empty twin        | West US -> East US | 697

As written in the documentation, it has been possible to conclude that the biggest driver for RTO change is the number of devices. 

Neither changing the twin size, nor the failover direction (East US -> West US or vice versa) was affecting the RTO in a significant way.

Of course there could be underneath changes of Azure IoT Hub or you may be considering transitioning towards a different number of units or a different IoT Hub SKU. Because of this, the RTO can change over time.

In your CI-pipeline for a separate test environment, you may consider adding an integration test, that verifies the behaviour of properly handling IoT Hub failover by your application.