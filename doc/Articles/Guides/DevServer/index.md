# Development Servers

During this guide we will set up a dedicated server and place the server build of our project on the dedicated server.

**IMPORTANT:** Before we begin, there are some potential problems you may face, as no server is truly 'free':

1. Service providers are NOT free.
2. Most of the time you can start with free trials for a limited time, after x amount of time or x amount of used resources the trial will end and you might incur payment.
3. Always read the providers free trial limitations.
4. Some providers require a payment method for using a Windows instance, however as long as you do not go over the limitations the provider should not bill you.

>   **NOTE**: Mirror is not affiliated and can not be held responsible for any charges or fees associated with service providers like
[AWS](https://aws.amazon.com/),
[Microsoft Azure](https://azure.microsoft.com/en-us/free/),
[Google Compute Engine](https://cloud.google.com/compute/) and others...

## Introduction

During your development with Mirror you will need to test your project as a client and as a server.
There are a few possible ways to test your project:

1. Default build: Host/client as one and connecting with another build/editor to the host locally on 1 computer.
2. Server build: Server is a separate executable. You can place it on your computer run it and connect to it as a client.
3. Dedicated Server: Same as the server build but placed on an external machine, you connect to it with the server's external ip.

This guide will focus on the "Dedicated Server" option. There are multiple providers and even self hosted dedicated machines.
All possibilities still go through the same process to ensure connectivity to clients. A few requirements for a dedicated server:

1. Port forwarding (Not strictly necessary but makes everything a lot easier without needing NAT punchthrough)
2. Firewall exceptions
3. Computer/machine that stays online and accessible whenever you need it.

In the upcoming sections we will go through setting up a provider with the free tier. 

**Always double check that you do not select a paid feature as this is purely meant as a short term period to test out basic functionality during development of your project.**

### Microsoft Azure

Microsoft Azure (as of time of writing) allows setting up a windows instance during the free trial without an active payment method.

**To be added**

### Google Compute Engine

Google Compute Engine (as of time of writing) does not allow setting up a windows instance during the free trial without an active payment method.

**To be added**

### Self Hosted Dedicated Server

A self hosted dedicated server is the same as the other providers but you provide the hardware, internet connection and costs for running the computer.
Most of the time this is the cheapest option during development if you already have an extra machine.
Preferably you would put the machine on a different network (to simulate the conditions as the other providers).
This would mean you could connect to the machine and put your server build on whenever you need and have access to the router and security settings of the machine for port forwarding and firewall exceptions.

In essence, this is the simplest set up but does require extra hardware.
