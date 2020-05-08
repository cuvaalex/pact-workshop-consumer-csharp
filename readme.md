# Step 2: Provider (Verifying an existing contract)
When we previously ran (in the consumer) the ```` ConsumerPactTests```` test, it passed, but it also generated a ````pacts/consumer-provider.json```` pact file that we can use to validate our assumptions in the provider side.
Pact has a rake task to verify the provider against the generated pact file. It can get the pact file from any URL (like the last successful CI build), but we are just going to use the local one for now.

Add under  file the following line require 'pact/tasks' so it looks like this:

With your Consumer Pact Test passing and your new Pact file we can now create the Provider
Pact test which will validate your mocked responses match actual responses from the
Provider API.

## Step 2.1 - Testing the Provider Project with Pact

Navigate to the ```[RepositoryRoot]/Provider/tests``` directory in your command line and create another new XUnit project by running the command
```dotnet new xunit```. Once again you will also need to add the correct version of the PactNet package using one of the command line commands below:

```
# Windows
dotnet add package PactNet.Windows --version 2.2.1

# OSX
dotnet add package PactNet.OSX --version 2.2.1

# Linux
dotnet add package PactNet.Linux.x64 --version 2.2.1
# Or...
dotnet add package PactNet.Linux.x86 --version 2.2.1
```

Finally your Provider Pact Test project will need to run its own web server during tests
which will be covered in more detail in the next step but for now, let's get the
```Microsoft.AspNetCore.All``` package which we will need to run this server. Run the 
command below to add it to your project:

```
dotnet add package Microsoft.AspNetCore.All --version 2.0.3
```

With all the packages added to our Provider API test project, we are ready to move onto
the next step; creating an HTTP Server to manage test environment state.

## Step 2.2 - Creating a Provider State HTTP Server

The Pact tests for the Provider API will need to do two things:

1. Manage the state of the Provider API as dictated by the Pact file.
2. Communicate with the Provider API to verify that the real responses for HTTP requests
defined in the Pact file match the mocked ones.

For the first point, we need to create an HTTP API used exclusively by our tests to manage
the transitions in the state. The first step is to create a simple web api that is started
when your test run starts.

#### Step 2.2.1 - Creating a Basic Web API to Manage Provider State

First, navigate to your new Provider Tests project
(```[RepositoryRoot]/Provider/tests/```) and create a file and corresponding class called ```TestStartup.cs```. In which we will create a basic Web API using the code
below:

``` csharp
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using provider.tests.Middleware;
    using Microsoft.AspNetCore.Hosting;

    namespace provider.tests
    {
        public class TestStartup
        {
            public TestStartup(IConfiguration configuration)
            {
                Configuration = configuration;
            }

            public IConfiguration Configuration { get; }

            // This method gets called by the runtime. Use this method to add services to the container.
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddMvc();
            }

            // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseMiddleware<ProviderStateMiddleware>();
                app.UseMvc();
            }
        }
    }
```

When you created the class above you might have noticed that the compiler has found a
compilation error because we haven't created the ProviderStateMiddleware class yet.

#### Step 2.2.2 - Creating a The Provider State Middleware

When creating a Pact test for a Provider your test needs its own API. The reason for
this is so it can manage the state of your API based on what the Pact file needs for each
request. This might be actions like ensuring a user is in the database or a user has
permission to access a resource.

Above we took the first step to create this API for our tests to access but currently
it both doesn't compile and even if we removed the ```app.UseMiddleware``` line it 
wouldn't do anything. We need to create a way for the API to manage the states required
by our tests. We will do this by creating a piece of middleware (similar to a controller)
that handles requests to the path ```/provider-states```.

#### Step 2.2.2.1 - Creating the ProviderState Class

First create a new folder at ```[RepositoryRoot]/Provider/tests/Middleware```
and create a file and corresponding class called ```ProviderState.cs``` and add the
following code:

```csharp
namespace provider.tests.Middleware
{
    public class ProviderState
    {
        public string Consumer { get; set; }
        public string State { get; set; }
    }
}
```

This is a simple class which represents the data sent to the ```/provider-states``` path.
The first property will store the name of *Consumer* who is requesting the state change.
Which in our case is **Consumer**. The second property stores the state we want the
Provider API to be in.

With this class in place, we can create the middleware class.

#### Step 2.2.2.2 - Creating the ProviderStateMiddleware Class

Again at ```[RepositoryRoot]/Provider/tests/Middleware``` create a file and corresponding class called ```ProviderStateMiddleware.cs```. For now add the following code:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Newtonsoft.Json;

namespace provider.tests.Middleware
{
    public class ProviderStateMiddleware
    {
        private const string ConsumerName = "Consumer";
        private readonly RequestDelegate _next;
        private readonly IDictionary<string, Action> _providerStates;

        public ProviderStateMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.Value == "/provider-states")
            {
                this.HandleProviderStatesRequest(context);
                await context.Response.WriteAsync(String.Empty);
            }
            else
            {
                await this._next(context);
            }
        }

        private void HandleProviderStatesRequest(HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            if (context.Request.Method.ToUpper() == HttpMethod.Post.ToString().ToUpper() &&
                context.Request.Body != null)
            {
                string jsonRequestBody = String.Empty;
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                {
                    jsonRequestBody = reader.ReadToEnd();
                }

                var providerState = JsonConvert.DeserializeObject<ProviderState>(jsonRequestBody);

                //A null or empty provider state key must be handled
                if (providerState != null && !String.IsNullOrEmpty(providerState.State) &&
                    providerState.Consumer == ConsumerName)
                {
                    _providerStates[providerState.State].Invoke();
                }
            }
        }
    }
}
```

The code above gives us a way to handle requests to the ```/provider-states``` path and
based on the ```ProviderState.State``` requested run some associated code but in the code
above the ```_providerStates``` is empty so let's update the constructor to set up two states
and the associated code. The states to be added are:

1. "There is data"

This state will create a text file called ```somedata.txt``` at
```[RepositoryRoot]/data```. This state is currently used by our Consumer
Pact test.

2. "There is no data"

This state will delete the text file ```somedata.txt``` at
```[RepositoryRoot]/data``` if it exists. This state is not currently used
by our Consumer Pact test but could be if some more test cases were added ;).

The code for this looks like:

```csharp
public class ProviderStateMiddleware
{
    private const string ConsumerName = "Consumer";
    private readonly RequestDelegate _next;
    private readonly IDictionary<string, Action> _providerStates;

    public ProviderStateMiddleware(RequestDelegate next)
    {
        _next = next;
        _providerStates = new Dictionary<string, Action>
        {
            {
                "There is no data",
                RemoveAllData
            },
            {
                "There is data",
                AddData
            }
        };
    }

    private void RemoveAllData()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), @"../../../../../data");
        var deletePath = Path.Combine(path, "somedata.txt");

        if (File.Exists(deletePath))
        {
            File.Delete(deletePath);
        }
    }

    private void AddData()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), @"../../../../../data");
        var writePath = Path.Combine(path, "somedata.txt");

        if (!File.Exists(writePath))
        {
            File.Create(writePath);
        }
    }
```

Now we have initialised our ```_providerStates``` field with the two states which map to
```AddData()``` and ```RemoveAllData()``` respectively. Now if our Consumer Pact test
contains the step:

```csharp
    _mockProviderService.Given("There is data");
```

When setting up a mock request our Provider API Pact test will map this to the
```AddData()``` method and create the ```somedata.txt``` file if it does not already exist.
If the mock defines the Given step as:

```csharp
    _mockProviderService.Given("There is no data");
```

Then the ```RemoveAllData()``` method will be called and if the ```somedata.txt``` file
exists it will be deleted.

With this code in place the ```ProviderStateMiddleware``` class should be completed and
look like:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Newtonsoft.Json;

namespace provider.tests.Middleware
{
    public class ProviderStateMiddleware
    {
        private const string ConsumerName = "Consumer";
        private readonly RequestDelegate _next;
        private readonly IDictionary<string, Action> _providerStates;

        public ProviderStateMiddleware(RequestDelegate next)
        {
            _next = next;
            _providerStates = new Dictionary<string, Action>
            {
                {
                    "There is no data",
                    RemoveAllData
                },
                {
                    "There is data",
                    AddData
                }
            };
        }

        private void RemoveAllData()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), @"../../../../../data");
            var deletePath = Path.Combine(path, "somedata.txt");

            if (File.Exists(deletePath))
            {
                File.Delete(deletePath);
            }
        }

        private void AddData()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), @"../../../../../data");
            var writePath = Path.Combine(path, "somedata.txt");

            if (!File.Exists(writePath))
            {
                File.Create(writePath);
            }
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.Value == "/provider-states")
            {
                this.HandleProviderStatesRequest(context);
                await context.Response.WriteAsync(String.Empty);
            }
            else
            {
                await this._next(context);
            }
        }

        private void HandleProviderStatesRequest(HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            if (context.Request.Method.ToUpper() == HttpMethod.Post.ToString().ToUpper() &&
                context.Request.Body != null)
            {
                string jsonRequestBody = String.Empty;
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                {
                    jsonRequestBody = reader.ReadToEnd();
                }

                var providerState = JsonConvert.DeserializeObject<ProviderState>(jsonRequestBody);

                //A null or empty provider state key must be handled
                if (providerState != null && !String.IsNullOrEmpty(providerState.State) &&
                    providerState.Consumer == ConsumerName)
                {
                    _providerStates[providerState.State].Invoke();
                }
            }
        }
    }
}
```

### Step 2.2.3 - Starting the Provider States API When the Pact Tests Start

Now we have a Provider States API we need to start it when our Provider Pact tests start.
To do this first rename the provided test class when you created the XUnit project to
```ProviderApiTests.cs``` and include the code below:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using PactNet;
using PactNet.Infrastructure.Outputters;
using tests.XUnitHelpers;
using Xunit;
using Xunit.Abstractions;

namespace provider.tests
{
    public class ProviderApiTests : IDisposable
    {
        private string _providerUri { get; }
        private string _pactServiceUri { get; }
        private IWebHost _webHost { get; }
        private ITestOutputHelper _outputHelper { get; }

        public ProviderApiTests(ITestOutputHelper output)
        {
            _outputHelper = output;
            _providerUri = "http://localhost:9000";
            _pactServiceUri = "http://localhost:9001";

            _webHost = WebHost.CreateDefaultBuilder()
                .UseUrls(_pactServiceUri)
                .UseStartup<TestStartup>()
                .Build();

            _webHost.Start();
        }

        [Fact]
        public void EnsureProviderApiHonoursPactWithConsumer()
        {
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _webHost.StopAsync().GetAwaiter().GetResult();
                    _webHost.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
```

Reading the code above when the ```EnsureProviderApiHonoursPactWithConsumer()``` test is
run using the ```dotnet test``` command at the command line the constructor will create
a new HTTP server with our Provider States API and store it in the ```_webHost``` property.
Once stored it will start the server so now our test once written can send requests to
```http://localhost:9001/provider-states``` to manipulate the state of our Provider API.

There are two other things to note:

* The Class implements IDisposable to ensure our Provider States API server is stopped
when the test is completed.
* The test requires a running instance of the Provider API server to verify the
responses match those expected in the Pact file. This server is not started by the tests.

## Step 2.3 - Creating the Provider API Pact Test

With our Provider States API in place and managed by our test when it is run we can
complete our test. Update the ```EnsureProviderApiHonoursPactWithConsumer()``` test
to:

```csharp
[Fact]
public void EnsureProviderApiHonoursPactWithConsumer()
{
    // Arrange
    var config = new PactVerifierConfig
    {

        // NOTE: We default to using a ConsoleOutput,
        // however xUnit 2 does not capture the console output,
        // so a custom outputter is required.
        Outputters = new List<IOutput>
                        {
                            new XUnitOutput(_outputHelper)
                        },

        // Output verbose verification logs to the test output
        Verbose = true
    };

    //Act / Assert
    IPactVerifier pactVerifier = new PactVerifier(config);
    pactVerifier.ProviderState($"{_pactServiceUri}/provider-states")
        .ServiceProvider("Provider", _providerUri)
        .HonoursPactWith("Consumer")
        .PactUri(@"..\..\..\..\..\pacts\consumer-provider.json")
        .Verify();
}
```

The **Act/Assert** part of this test creates a new
[PactVerifier](https://github.com/pact-foundation/pact-net/blob/master/PactNet/PactVerifier.cs)
instance which first uses a call to ```ProviderState``` to know where our Provider States
API is hosted. Next, the ```ServiceProvider``` method takes the name of the Provider being
verified in our case **Provider** and a URI to where it is hosted. Then the
```HonoursPactWith()``` method tells Pact the name of the consumer that generated the Pact
which needs to be verified with the Provider API - in our case **Consumer**.  Finally, in
our workshop, we point Pact directly to the Pact File (instead of hosting elsewhere) and 
call ```Verify``` to test that the mocked request and responses in the Pact file for our
Consumer and Provider match the real responses from the Provider API.

However there is one last step - the test currently doesn't compile as the
```XUnitOutput``` class does not exist - so we will create it.

### Step 2.3.1 - Creating the XUnitOutput Class

As noted by the comment in ```ProviderApiTests``` XUnit doesn't capture the output we want
to show in the console to tell us if a test run as passed or failed. So first create the
folder ```[RepositoryRoot]/Provider/tests/XUnitHelpers``` and inside create
the file ```XUnitOutput.cs``` and the corresponding class which should look like:

```csharp
using PactNet.Infrastructure.Outputters;
using Xunit.Abstractions;

namespace provider.tests.XUnitHelpers
{
    public class XUnitOutput : IOutput
    {
        private readonly ITestOutputHelper _output;

        public XUnitOutput(ITestOutputHelper output)
        {
            _output = output;
        }

        public void WriteLine(string line)
        {
            _output.WriteLine(line);
        }
    }
}
```

This class will ensure the output from Pact is displayed in the console. How this works
is beyond the scope of this workshop but you can read more at
[Capturing Output](https://xunit.github.io/docs/capturing-output.html).

## Step 2.4 - Running Your Provider API Pact Test

Now we have a test in the Consumer Project which creates our Pact file based on its mock
requests to the Provider API and we have a Pact test in the Provider API which consumes
this Pact file to verify the mocks match the actual responses we should run the Provider
tests!

### Step 2.4.1 - Start Your Provider API Locally

In the command line navigate to ```[RepositoryRoot]/Provider/src``` and run
the command below to start the server:

```
dotnet run
```

This should show ouput similar to:

```
YourPC:src thomas.shipley$ dotnet run
Using launch settings from /Users/thomas.shipley/code/thomas/pact-workshop-dotnet-core-v1/YourSolution/Provider/src/Properties/launchSettings.json...
Hosting environment: Development
Content root path: /Users/thomas.shipley/code/thomas/pact-workshop-dotnet-core-v1/YourSolution/Provider/src
Now listening on: http://localhost:9000
Application started. Press Ctrl+C to shut down.
```

If you see the output above leave that server running and move on to the next step!

### Step 4.3.2 - Run your Provider API Pact Test

First, confirm you have a Pact file at ```[RepositoryRoot]/YourSolution/pacts``` called
consumer-provider.json.

Next, create another command line window and navigate to
```[RepositoryRoot]/YourSolution/Provider/tests``` and to run the tests type in and execute
the command below:

```
dotnet test
```

Once you run this command and it completes you will hopefully see some output which looks like:

```
YourPC:tests thomas.shipley$ dotnet test
Build started, please wait...
Build completed.

Test run for /Users/thomas.shipley/code/thomas/pact-workshop-dotnet-core-v1/YourSolution/Provider/tests/bin/Debug/netcoreapp2.0/tests.dll(.NETCoreApp,Version=v2.0)
Microsoft (R) Test Execution Command Line Tool Version 15.3.0-preview-20170628-02
Copyright (c) Microsoft Corporation.  All rights reserved.

Starting test execution, please wait...
[xUnit.net 00:00:03.1234490]   Discovering: tests
[xUnit.net 00:00:03.2294800]   Discovered:  tests
[xUnit.net 00:00:03.2992030]   Starting:    tests
info: Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager[0]
      User profile is available. Using '/Users/thomas.shipley/.aspnet/DataProtection-Keys' as key repository; keys will not be encrypted at rest.
info: Microsoft.AspNetCore.Hosting.Internal.WebHost[1]
      Request starting HTTP/1.1 POST http://localhost:9001/provider-states application/json 74
info: Microsoft.AspNetCore.Hosting.Internal.WebHost[2]
      Request finished in 24.308ms 200 
info: Microsoft.AspNetCore.Hosting.Internal.WebHost[1]
      Request starting HTTP/1.1 POST http://localhost:9001/provider-states application/json 80
info: Microsoft.AspNetCore.Hosting.Internal.WebHost[2]
      Request finished in 1.849ms 200 
info: Microsoft.AspNetCore.Hosting.Internal.WebHost[1]
      Request starting HTTP/1.1 POST http://localhost:9001/provider-states application/json 74
info: Microsoft.AspNetCore.Hosting.Internal.WebHost[2]
      Request finished in 0.476ms 200 
info: Microsoft.AspNetCore.Hosting.Internal.WebHost[1]
      Request starting HTTP/1.1 POST http://localhost:9001/provider-states application/json 74
info: Microsoft.AspNetCore.Hosting.Internal.WebHost[2]
      Request finished in 0.217ms 200 
[xUnit.net 00:00:06.8562100]   Finished:    tests

Total tests: 1. Passed: 1. Failed: 0. Skipped: 0.
Test Run Successful.
Test execution time: 7.9642 Seconds
```

Hopefully, you see the above output which means your Pact Provider test was successful!
At this point, you now have a working local example of a Pact test suite that tests
both the Consumer and Provider sides of an application but a few test cases are
missing...

Now run ```` git checkout step3 ```` to go to the next step
