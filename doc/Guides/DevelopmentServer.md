# Development Servers

During this guide we will set up a dedicated server and place the server build of our project on the dedicated server.

**IMPORTANT: Service providers are NOT free. Take note that Mirror is not affiliated and can not be held responsible for any charges or
fees associated with service providers like [AWS](https://aws.amazon.com/), [Microsoft Azure](https://azure.microsoft.com/en-us/free/), 
[Google Compute Engine](https://cloud.google.com/compute/) and others...
Most of the time you can start with free trials for a limited time, after x amount of time or x amount of used resources the trial will end and you might incur payment.
Always read the providers free trial limitations.
Some providers require a creditcard for using a windows instance, as long as you do not go over the limitations the provider should not bill you.**

During your development with Mirror you will need to test your project as a client and as a server.
There are a few possible ways to test your project:

1. Default build    =>  Host/client as one and connecting with another build/editor to the host locally on 1 computer.
2. Server build     =>  Server is a separate executable. You can place it on your computer run it and connect to it as a client.
3. Dedicated Server =>  Same as the server build but placed on an external machine, you connect to it with the computer's external ip.

This guide will focus on the "Dedicated Server" option. There are multiple providers and even self hosted dedicated machines.
All possibilities still go through the same process to ensure connectivity to clients. A few requirements for a dedicated server:

1. Port forwarding (Not strictly necessary but makes everything a lot easier without needing NAT punchthrough)
2. Firewall exceptions
3. Computer/machine that stays online and accessible whenever you need it.

In the upcoming sections we will go through setting up a provider with the free tier. 

**Please always double check when in doubt that you do not select a paid feature as this is purely meant as a short term period 
to test out basic functionality during development of your project.**

## Amazon Web Services (AWS)

During this section we will focus on using a windows instance and connecting from a windows computer.

**Please note: AWS requires a creditcard added before being able to use a windows server.**

Don't forget to read up on the free tier limitations [HERE](https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/billing-free-tier.html).
During this example we will be using the free tier of the EC2 option, which gives us the possibility to use a windows instance (if added a payment method).
For more information about which services offer a Free Tier, see [AWS Free Tier](https://aws.amazon.com/free/?all-free-tier.sort-by=item.additionalFields.SortRank&all-free-tier.sort-order=asc&awsf.Free%20Tier%20Types=tier%2312monthsfree&awsf.Free%20Tier%20Categories=categories%23compute).

These are the steps we will go through from start to finish.

1. Account creation
2. Setting up an instance with the EC2 Management Console
3. Configuring the RDP(Remote Desktop Program)
4. Setting up the firewall to allow connections through
5. Testing the connection

**1. Account Creation**

Go to the [Account creation page](https://portal.aws.amazon.com/billing/signup?nc2=h_ct&src=default&redirect_url=https%3A%2F%2Faws.amazon.com%2Fregistration-confirmation#/start) and
sign up with your details and payment method (creditcard for example). Adding the payment method is necessary to run a windows instance on AWS.

**2. Setting up an instance with the EC2 Management Console**
After setting up your account you should be logged in.
Always make sure you're in your correct region so it's a good habit to check in the top right corner. Change it to the closest location to you now.
After this click on Services in the top left corner and click on EC2. This will bring you to the EC2 instance dashboard and ready to Launch your instance!

Click on **"Launch Instance"**.
![alt text](https://i.imgur.com/UlKW8qW.png)

There are 7 steps to creating your AWS Instance:

1. Choose an Amazon Machine Image (AMI)
2. Choose Instance Type
3. Configure Instance
4. Add Storage
5. Add Tags
6. Configure Security Group
7. Review

* Choose an Amazon Machine Image

Scroll down until you see the Microsoft Windows Server 2019 Base (take the newest one if this is outdated).
Make sure that the option you select has the "Free Tier Eligible" under the icon and click on "Select".
![alt text](https://i.imgur.com/v0Y3cmG.png)

* Choose Instance Type

Make sure you choose the instance type with the "free tier eligible", at the time of writing this is the t2.micro instance.
Click on **"Next: Configure Instance Details"**. Do **NOT** click on Review and Launch.

![alt text](https://i.imgur.com/uCdu34j.png)

* Configure Instance

Nothing has to be changed at this step. Keep it default. Click on **"Next: Add Storage**.

* Add Storage

Nothing has to be changed. Keep it default. The 30GB is more than you need but there will be an error if you choose a lower amount.
Keep in mind that you can always click "Previous" if you need to return, you do not need to repeat the entire process if you make a mistake (unless you click Launch at the end).

Click on **"Next: Add Tags** to continue.

* Add Tags

Nothing has to be changed. Keep it default. Click on **"Next: Configure Security Groups"**

* Configure Security Groups

**!!IMPORTANT STEP!! This makes it possible to connect to your instance from outside for SSH(if needed), 
RDP(NEEDED DURING THIS EXAMPLE) and later for clients connecting through port 7777 for your Mirror Project.**

Create a new security group and you can give it your own name and description.
Add the following rules:

RDP with source "Anywhere", Description can be whatever but put it as Remote Desktop Program.
Custom TCP Rule with port 7777 and source "Anywhere", Description can be whatever but put it as Mirror.
SSH with source "Anywhere", Description can be whatever but put it as SSH.

Note: SSH is not strictly necessary but can be used to remote connect to it through other means than the RDP.

You can ignore the warning about the source as this is just a testing environment. 
In the future you might wish to restrict this but you will almost never know the clients connection IP beforehand.

![alt text](https://i.imgur.com/4xJQt5L.png)

Click on **Next: Review and Launch"**

* Review

Almost there! Make sure everything is correct and you are using the free tier, then click on **"Launch"**.
![alt text](https://i.imgur.com/jcwooNO.png)

1 more thing! A window will pop up, asking for your key pair. Just create a new one by selecting the dropdown: **"Create a new key pair"** and
give it a name, click on **"Download Key Pair"**.
Keep the key file (.PEM File) somewhere secure (To be 100% certain, back it up somewhere). **YOU CAN NOT ACCESS THE CREATED INSTANCE WITHOUT THIS KEY**

![alt text](https://i.imgur.com/ZP6eJq5.png)

Now you can (finally) click on **Launch Instances**!

Go back to your EC2 dashboard by clicking on "Services" at the top left and clicking on EC2.
Now you see you have "Running Instances: 1". **Click on "Running Instances" to continue**.

![alt text](https://i.imgur.com/qz4YwgB.png)

You now see your instance running! If it's still initializing, give it a few minutes! A new instance might take around 5-10min to set up.
Refresh the page after 10 minutes if nothing changes.

Now you did all this but you want to get ON the dedicated server right? Perfect! The next step will get you up and running!

**3. Configuring the RDP(Remote Desktop Program)**

Time to get the RDP file so you can start connecting!
There are a few things we'll need!

1. RDP file with the key pair added to it
2. Configure RDP file once downloaded to allow getting files from our C: drive or other drives (so you can easily get your zip project.
3. Enter the windows Admin password once you start the RDP file

Once you've doen this, you should be able to continue using the same RDP file.

* 1. RDP file with the key pair added to it
![alt text](https://i.imgur.com/b7YGhX4.png)

**BEFORE CLICKING DOWNLOAD GET THE PASSWORD**

![alt text](https://i.imgur.com/yTqKryT.png)

**COPY THE PASSWORD FOR LATER**

Now click on **"Download Remote Desktop File"**

The RDP file will be downloaded.

![alt text](https://i.imgur.com/SJ0ER7y.png)

* 2. Configure RDP file for easy file access

Go to your freshly downloaded RDP file and rightclick it and then click "Edit".

Go to the third tab "Local sources", at the bottom click "more" under local devices and sources. 
On the new window select your C: drive or any other drive. This is your own computer your connecting from. For easy file exchange.

![alt text](https://i.imgur.com/2U4MLkS.png)

Perfect! Now you can run the RDP file! The RDP file will ask a password. If you forgot your password you can get it back by rightclicking 
the instance and clicking on "Get Windows Password". You will be asked to re-enter your key pair (.PEM) file and decrypt the message.
Once done you will be able to copy the password.

![alt text](https://i.imgur.com/evD4c6s.png)

![alt text](https://i.imgur.com/cYjS94u.png)

There you have it! Now you 100% have your password and you should be logging into your dedicated server!

* 4. Setting up the firewall to allow connections through

Go to the windows firewall settings, go to the advanced firewall settings and go to inbound rules. **Add a new rule** and choose the port type.
Select TCP and enter the 7777 port (or any other if you use another port in Mirror). Continue clicking next and keep things default.
Close all the windows when done.

![alt text](https://i.imgur.com/SkckL5e.png)

AWESOME! You now have everything set up to accept incoming requests on **port 7777**.

* 5. Testing the connection

Before you can (finally) test out your server build of your project you need to get it on the dedicated server!

Place your (zipped) server build at the root of your added drive (C: or another) to make it easier to find it fast.

Go to **"My Computer"** and because of our previous changes to the RDP we should now see your local drive under "Devices and Drives".
Double click it and because you placed your zipped server build on that drive, you should see it immediatly when it's done loading.

![alt text](https://i.imgur.com/4kdgfMx.png)

Now **unzip** the project in a new folder on the dedicated server's desktop and you can now **run it!**

Want to test if it sees the 7777 port is open **AFTER YOUR STARTED YOUR MIRROR SERVER**?
Get your IPv4 public IP from the EC2 Management Console from your instance and use it on your client to connect to that IP.

Go to [PortChecker](https://www.portcheckers.com/) and enter the Dedicated Server IP address and enter port 7777.

**PLEASE NOTE: IF YOU ARE NOT RUNNING YOUR GAME/PROJECT THEN THE PORT WILL BE CLOSED, IT'S ONLY OPEN WHEN YOU ARE RUNNING SERVER BUILD!**


## Microsoft Azure

Microsoft Azure (as of time of writing) allows setting up a windows instance during the free trial without an active payment method.

**To be added**

## Google Compute Engine

Google Compute Engine (as of time of writing) does **NOT** allow setting up a windows instance during the free trial without an active payment method.

**To be added**

## Self Hosted Dedicated Server

A self hosted dedicated server is the same as the other providers but you provide the hardware, internet connection and costs for running the computer.
Most of the time this is the "cheapest" option during development **if** you already have an extra machine.
Preferably you would put the machine on a different network (to simulate the conditions as the other providers).
This would mean you could connect to the machine and put your server build on whenever you need and have access to the router and security settings of the machine for port forwarding and firewall exceptions.

In essence, this is the simplest set up but does require extra hardware
