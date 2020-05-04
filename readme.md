This project was created to present the reader how Pact.Net is used with .Net Core 3.1

Before starting I wish to thanks the following github authors where I took part of their ideas to create this repository:
* [Doctor500](https://github.com/doktor500/pact-workshop-consumer): for his ideas how to organise the repository for a training by steps
* [tdshipley](https://github.com/tdshipley/pact-workshop-dotnet-core-v1): for having a full working solution on .Net core 2

# Pre-Requirements
* Fork this github repository into your account (You will find a "fork" icon on the top right corner)
* Clone the forked repository that exists in your github account into your local machine

# Requirements
* .Net Core 3.1 is already installed on your system
* Use what ever IDE you like

# Step 1
## Step 1.1 - Start the Provider API Locally
Using the command line to navigate
```` 
[RepositoryRoot]/Provider/src/
```` 
Once in the Provider */src/* directory first do a ```dotnet restore``` at the command line to pull down the dependencies required for the project.
Once that has completed run ```dotnet run``` this will start your the Provider API. Now check that everything is working O.K. by navigating to
the URL below in your browser:

```
http://localhost:9000/api/provider?validDateTime=05/01/2018
```

If your request is successful you should see in your browser:

```
{"test":"NO","validDateTime":"05-01-2018 00:00:00"}
```

If you see the above leave the Provider API running then you are ready to try out the consumer.

### Step 1.2 - Execute the Consumer

With the Provider API running open another command line instance and navigate to:

```
[RepositoryRoot]/Consumer/src/
```

Once in the directory run another ```dotnet restore``` to pull down the dependencies for the Consumer project. Once this is completed at the command line
type in ```dotnet run``` you should see output:

```
MyPc:src >$ dotnet run
-------------------
Running consumer with args: dateTimeToValidate = 05/01/2018, baseUri = http://localhost:9000
To use with your own parameters:
Usage: dotnet run [DateTime To Validate] [Provider Api Uri]
Usage Example: dotnet run 01/01/2018 http://localhost:9000
-------------------
Validating date...
{"test":"NO","validDateTime":"05-01-2018 00:00:00"}
...Date validation complete. Goodbye.
```

If you see output similar to above in your command line then the consumer is now running successfully! If you want to now you can experiment with passing in
parameters different to the defaults.

We are now ready to start the testing with Pact

Run ```git checkout step1``` and follow the instructions in this readme file

