## Cross-region processing latencies

Assuming the following architecture:

[![](assets/sample-architecture.jpeg)](assets/sample-architecture.jpeg)

There are some IoT Devices, sending data to an Azure IoT Hub. These telemetry data get collected, processed and then stored by multiple pods running in an Azure Kubernetes Service cluster.

For this exploration we are taking in consideration a scenario where is not the entire region to be affected by a temporary downtime and you only want to failover IoT Hub service.

In other failover scenarios you may want to have every service running in the same region, including the ones that are processing telemetry data ingested by IoT Hub. 

The last scenario, though, is out of scope for the purpose of this spike.

The question we tried to answer is: what happens to latency when processing messages cross-region due to a IoT Hub **only** manual failover?

Multiple tests were executed in order to understand how it is affected the latency between the IoT Hub enqueue time and collection time in AKS cluster.

To produce simulated telemetry from AKS cluster, we used this [telemetry simulator](https://github.com/fbeltrao/iot-telemetry-simulator), with the following options:

| Options           | Value      |
| ----------------- | ---------- |
| msg / sec         | 3000       |
| message size (KB) | 0.5 and 1  |

On the consumer side, the following parameters have been used for a Event Host Processor running as a pod in an Azure Kubernetes Services cluster:

| Parameter name    | Value   |
| ----------------- | ------- |
| MaxBatchSize      | 1000    |
| PrefetchCount     | 2000    |

We ran the experiment in the two following scenarios:

1. Failover **only** the IoT Hub to the paired region, leaving the remaining parts of the architecture in the original region.
1. Failover IoT Hub **and** the Kubernetes cluster to the paired region.

The results of the tests show that moving IoT Hub only is adding roughly 200ms of latency (95th percentile).

Even if it's true that keeping everything in the secondary region reduces the additional latency introduced from the failover, there is some additional complexity due to the fact that the processing application and its dependencies need to be deployed across multiple regions. 

For many reasons, like on-the-fly cluster or application deployment issues, you may want to always have the AKS cluster running in the secondary paired region.
This approach introduces also the cost of always running **two** Azure Kubernetes Service clusters at the same time.

Of course, immediate visible added latency and increased costs are only two consequences of having the services span across regions. For example, you should consider having an added health check for the solution in the secondary region and you should have a plan on how to revert to primary original region when things get stable again.

Therefore, definitely, you should not underestimate the effort of replicating the solution on a secondary region.