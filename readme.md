# Step 1: Consumer (Creating the first Contract)

At this stage if we want to validate our Consumer is always working, we have 3 choices:
1. Create an End to End test that involve the Consumer and the Provider
2. Creating an Integration test for the API that Provider is exposing
3. Implement a Contract test

Creating and E2E test is expensive since in a CD environment you will need to have instances of both microservices running in order to execute the test.

Creating an Integration test for the API that Provider service is exposing is a good alternative but it has some drawbacks.

If the test is written in the provider side, if the API changes it is going to be difficult to make the consumer aware of the change
If the test is written on the consumer side, you will need an instance of the provider (Provider Service) running in order to be able to execute the test
We will explore Option 3, and we will implement a Contract test using [PactNet](https://github.com/pact-foundation/pact-net)

For this first step, we move to the ```Consumer/tests``` folder and we launch the following on the command line:

Pact cannot execute tests on its own it needs a test runner project. For this workshop, we will be using [XUnit](https://xunit.github.io/) to create the project
navigate to ```[RepositoryRoot]/YourSolution/Consumer/tests``` and run:

```
dotnet new xunit
```

This will create an empty XUnit project with all the references you need... expect Pact. Depending on what OS you are completing this workshop on you will need
to run one of the following commands:

```
# Windows
dotnet add package PactNet.Windows

# OSX
dotnet add package PactNet.OSX

# Linux
dotnet add package PactNet.Linux.x64
# Or...
dotnet add package PactNet.Linux.x86
```

Finally you will need to add a reference to the Consumer Client project src code. So again at the same command line type and run the command:

```
dotnet add reference ../src/consumer.csproj
```

This will allow you to access public code from the Consumer Client project which you will need to do to test the code!

Once this command runs successfully you will have in ```[RepositoryRoot]/Consumer/tests``` an empty .NET Core XUnit Project with Pact and we can begin to setup Pact!

#### NB - Multiple OS Environments

When using Pact tests for your production projects you might want to support multiple OSes. You can with .NET Core specify different packages in your
**.csproj** file based on the operating system but for the purpose of this workshop this is unnecessary. Other language implementations do not always
require OS based packages.

We also need to add our new xunit project to our solution
````
> cd ../../
> dotnet sln add Consumer/tests
````

## Step 1.1 - Configuring the Mock HTTP Pact Server on the Consumer

Pact works by placing a mock HTTP server between the consumer and provider(s) in an application to handle mocked provider interactions on the consumer
side and replay this actions on the provider side to verify them. So before we can write Pact tests we need to setup and configure this mock server.
This server will be used for all the tests in our Consumer test project.

XUnit shares common resources in a few different ways. For this workshop, we shall create a [Class Fixture](https://xunit.github.io/docs/shared-context.html)
which will share our mock HTTP server between our consumer tests. Start by creating a file and class called ```ConsumerPactClassFixture.cs``` in the root of
the Consumer test project (```[RepositoryRoot]/Consumer/tests```). It should look like:

```csharp
using System;
using Xunit;

namespace tests
{
    // This class is responsible for setting up a shared
    // mock server for Pact used by all the tests.
    // XUnit can use a Class Fixture for this.
    // See: https://goo.gl/hSq4nv
    public class ConsumerPactClassFixture
    {
    }
}
```

### Step 1.1.1 - Setup using PactBuilder

The [PactBuilder](https://github.com/pact-foundation/pact-net/blob/master/PactNet/PactBuilder.cs) is the class used to build out the configuration we
need for Pact which defines among other things where to find our mock HTTP server.

First, at the top of your class add some properties which will be used to store your instance of PactBuilder and store Mock HTTP Server properties:

```csharp
using System;
using Xunit;
using PactNet;
using PactNet.Mocks.MockHttpService;

namespace tests
{
    // This class is responsible for setting up a shared
    // mock server for Pact used by all the tests.
    // XUnit can use a Class Fixture for this.
    // See: https://goo.gl/hSq4nv
    public class ConsumerPactClassFixture
    {
        public IPactBuilder PactBuilder { get; private set; }
        public IMockProviderService MockProviderService { get; private set; }

        public int MockServerPort { get { return 9222; } }
        public string MockProviderServiceBaseUri { get { return String.Format("http://localhost:{0}", MockServerPort); } }
    }
}
```

Above we have setup some properties which ultimately say our Mock HTTP Server will be hosted at ```http://localhost:9222```. With that in place the next
step is to add a constructor to start the other properties starting with PactBuilder:

```csharp
using System;
using Xunit;
using PactNet;
using PactNet.Mocks.MockHttpService;

namespace tests
{
    // This class is responsible for setting up a shared
    // mock server for Pact used by all the tests.
    // XUnit can use a Class Fixture for this.
    // See: https://goo.gl/hSq4nv
    public class ConsumerPactClassFixture
    {
        public IPactBuilder PactBuilder { get; private set; }
        public IMockProviderService MockProviderService { get; private set; }

        public int MockServerPort { get { return 9222; } }
        public string MockProviderServiceBaseUri { get { return String.Format("http://localhost:{0}", MockServerPort); } }

        public ConsumerPactClassFixture()
        {
            // Using Spec version 2.0.0 more details at https://goo.gl/UrBSRc
            var pactConfig = new PactConfig
            {
                SpecificationVersion = "2.0.0",
                PactDir = @"..\..\..\..\..\pacts",
                LogDir = @".\pact_logs"
            };

            PactBuilder = new PactBuilder(pactConfig);

            PactBuilder.ServiceConsumer("Consumer")
                       .HasPactWith("Provider");
        }
    }
}
```

The constructor is doing a couple of things right now:

* It creates a [PactConfig](https://github.com/pact-foundation/pact-net/blob/master/PactNet/PactConfig.cs) object which allows us to specify:
  * The Pact files will be generated and overwritten too (```[RepositoryRoot]/pacts```).
  * The Pact Log files will be written to the executing directory.
  * The project will follow [Pact Specification](https://github.com/pact-foundation/pact-specification) 2.0.0
* Define the name of our Consumer project (Consumer) which will be used in other Pact Test projects.
  * Define the relationships our Consumer project has with others. In this case, just one called "Provider" this name will map to the same name used in the
  Provider Project Pact tests.

The final thing it needs to do is create an instance of our Mock HTTP service using the now created configuration:

```csharp
using System;
using Xunit;
using PactNet;
using PactNet.Mocks.MockHttpService;

namespace tests
{
    // This class is responsible for setting up a shared
    // mock server for Pact used by all the tests.
    // XUnit can use a Class Fixture for this.
    // See: https://goo.gl/hSq4nv
    public class ConsumerPactClassFixture
    {
        public IPactBuilder PactBuilder { get; private set; }
        public IMockProviderService MockProviderService { get; private set; }

        public int MockServerPort { get { return 9222; } }
        public string MockProviderServiceBaseUri { get { return String.Format("http://localhost:{0}", MockServerPort); } }

        public ConsumerPactClassFixture()
        {
            // Using Spec version 2.0.0 more details at https://goo.gl/UrBSRc
            var pactConfig = new PactConfig
            {
                SpecificationVersion = "2.0.0",
                PactDir = @"..\..\..\..\..\pacts",
                LogDir = @".\pact_logs"
            };

            PactBuilder = new PactBuilder(pactConfig);

            PactBuilder.ServiceConsumer("Consumer")
                       .HasPactWith("Provider");

            MockProviderService = PactBuilder.MockService(MockServerPort);
        }
    }
}
```

By adding the line ```MockProviderService = PactBuilder.MockService(MockServerPort);``` to the constructor we have created our Mock HTTP Server with
our specific configuration. We are nearly ready to start mocking out Provider interactions but (in my best Columbo voice) there is [just one more
thing](https://www.youtube.com/watch?v=biW9BbWJtQU).

### Step 1.1.2 Tearing Down the Pact Mock HTTP Server & Generating the Pact File

If the tests were to use the Class Fixture above as is right now the Mock Server might be left running once the tests have finished and worse no Pact file
would be created - so we wouldn't be able to verify our mocks with the Provider API!


It is always a good idea in your tests to teardown any resources used in them at end of the test run. However [XUnit doesn't implement teardown methods](http://mrshipley.com/2018/01/10/implementing-a-teardown-method-in-xunit/) so instead we can implement the IDisposable interface to handle the clean up
of the Mock HTTP Server which will at the same time generate our Pact file. To do this update your ConsumerPactClassFixture class to conform to IDisposable
and clean up the server using ```PactBuilder.Build()```:

```csharp
using System;
using Xunit;
using PactNet;
using PactNet.Mocks.MockHttpService;

namespace tests
{
    // This class is responsible for setting up a shared
    // mock server for Pact used by all the tests.
    // XUnit can use a Class Fixture for this.
    // See: https://goo.gl/hSq4nv
    public class ConsumerPactClassFixture : IDisposable
    {
        public IPactBuilder PactBuilder { get; private set; }
        public IMockProviderService MockProviderService { get; private set; }

        public int MockServerPort { get { return 9222; } }
        public string MockProviderServiceBaseUri { get { return String.Format("http://localhost:{0}", MockServerPort); } }

        public ConsumerPactClassFixture()
        {
            // Using Spec version 2.0.0 more details at https://goo.gl/UrBSRc
            var pactConfig = new PactConfig
            {
                SpecificationVersion = "2.0.0",
                PactDir = @"..\..\..\..\..\pacts",
                LogDir = @".\pact_logs"
            };

            PactBuilder = new PactBuilder(pactConfig);

            PactBuilder.ServiceConsumer("Consumer")
                       .HasPactWith("Provider");

            MockProviderService = PactBuilder.MockService(MockServerPort);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // This will save the pact file once finished.
                    PactBuilder.Build();
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

The ```PactBuilder.Build()``` method will teardown the Mock HTTP Server it uses for tests and generates the Pact File used for verifying mocks with
providers. It will always overwrite the Pact file with the results of the latest test run.

## Step 1.2 - Creating Your First Pact Test for the Consumer Client

With the class fixture created to manage the Mock HTTP Server update the test class added
by the ```dotnet new xunit``` command to be named ```ConsumerPactTests``` and update
the file name to match. With that done update the class to conform to the IClassFixture
interface and create an instance of your class fixture in the constructor.

```csharp
using System;
using Xunit;
using PactNet.Mocks.MockHttpService;
using PactNet.Mocks.MockHttpService.Models;
using Consumer;
using System.Collections.Generic;

namespace tests
{
    public class ConsumerPactTests : IClassFixture<ConsumerPactClassFixture>
    {
        private IMockProviderService _mockProviderService;
        private string _mockProviderServiceBaseUri;

        public ConsumerPactTests(ConsumerPactClassFixture fixture)
        {
            _mockProviderService = fixture.MockProviderService;
            _mockProviderService.ClearInteractions(); //NOTE: Clears any previously registered interactions before the test is run
            _mockProviderServiceBaseUri = fixture.MockProviderServiceBaseUri;
        }
    }
}
```

With an instance of our Mock HTTP Server in our test class, we can add the first test. 
All the Pact tests added during this workshop will follow the same three steps:

1. Mock out an interaction with the Provider API.
2. Interact with the mocked out interaction using our Consumer code.
3. Assert the result is what we expected.

For the first test, we shall check that if we pass an invalid date string to our Consumer
that the Provider API will return a ```400``` response and a message explaining why the
request is invalid.

### Step 1.2.1 - Mocking an Interaction with the Provider

Create a test in ```ConsumerPactTests``` called ```ItHandlesInvalidDateParam()``` and
using the code below mock out our HTTP request to the Provider API which will return a
```400```:

```csharp
[Fact]
public void ItHandlesInvalidDateParam()
{
    // Arange
    var invalidRequestMessage = "validDateTime is not a date or time";
    _mockProviderService.Given("There is data")
                        .UponReceiving("A invalid GET request for Date Validation with invalid date parameter")
                        .With(new ProviderServiceRequest 
                        {
                            Method = HttpVerb.Get,
                            Path = "/api/provider",
                            Query = "validDateTime=lolz"
                        })
                        .WillRespondWith(new ProviderServiceResponse {
                            Status = 400,
                            Headers = new Dictionary<string, object>
                            {
                                { "Content-Type", "application/json; charset=utf-8" }
                            },
                            Body = new 
                            {
                                message = invalidRequestMessage
                            }
                        });
}
```

The code above uses the ```_mockProviderService``` to setup our mocked response using Pact.
Breaking it down by the different method calls:

* ```Given("")```

This workshop will talk more about the Given method when writing the Provider API Pact test
but for now, it is important to know that the Given method manages the state that your test
requires to be in place before running. In our example, we require the Provider API to
have some data. The Provider API Pact test will parse these given statements and map
them to methods which will execute code to setup the required state(s).

* ```UponReceiving("")```

When this method executes it will add a description of what the mocked HTTP request
represents to the Pact file. It is important to be accurate here as this message is what
will be shown when a test fails to help a developer understand what went wrong.

* ```With(ProviderServiceRequest)```

Here is where the configuration for your mocked HTTP request is added. In our example
we have added what *Method* the request is (Get) the *Path* the request is made to 
(api/provider/) and the query parameters which in this test is our invalid date time
string (validDateTime=lolz).

* ```WillRespondWith(ProviderServiceResponse)```

Finally, in this method, we define what we expect back from the Provider API for our mocked
request. In our case a ```400``` HTTP Code and a message in the body explaining what the
failure was. 

All the methods above on running the test will generate a *Pact file* which will be used
by the Provider, API to make the same requests against the actual API to ensure the responses
match the expectations of the Consumer.

### Step 1.2.2 - Completing Your First Consumer Test

With the mocked response setup the rest of the test can be treated like any other test
you would write; perform an action and assert the result:

```csharp
[Fact]
public void ItHandlesInvalidDateParam()
{
    // Arange
    var invalidRequestMessage = "validDateTime is not a date or time";
    _mockProviderService.Given("There is data")
                        .UponReceiving("A invalid GET request for Date Validation with invalid date parameter")
                        .With(new ProviderServiceRequest 
                        {
                            Method = HttpVerb.Get,
                            Path = "/api/provider",
                            Query = "validDateTime=lolz"
                        })
                        .WillRespondWith(new ProviderServiceResponse {
                            Status = 400,
                            Headers = new Dictionary<string, object>
                            {
                                { "Content-Type", "application/json; charset=utf-8" }
                            },
                            Body = new 
                            {
                                message = invalidRequestMessage
                            }
                        });

    // Act
    var result = ConsumerApiClient.ValidateDateTimeUsingProviderApi("lolz", _mockProviderServiceBaseUri).GetAwaiter().GetResult();
    var resultBodyText = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();

    // Assert
    Assert.Contains(invalidRequestMessage, resultBodyText);
}
```

With the updated test above it will make a request using our Consumer client and get the
mocked interaction back which we assert on to confirm the error message is the one we
expect.

You can now launch your pact test as follow: ```` dotnet test ````

You should get the following results:

````
 Microsoft (R) Test Execution Command Line Tool Version 16.3.0
 Copyright (c) Microsoft Corporation.  All rights reserved.
 
 Starting test execution, please wait...
 
 A total of 1 test files matched the specified pattern.
 INFO  WEBrick 1.3.1
 INFO  ruby 2.2.2 (2015-04-13) [x86_64-darwin13]
 INFO  WEBrick::HTTPServer#start: pid=60033 port=9222
                                                                                                                                                                                                                                                                          
 Test Run Successful.
 Total tests: 1
      Passed: 1
 Total time: 2.5556 Seconds


````
 If you look on the root folder /pacts you will found the following json file:
 
````
{
  "consumer": {
    "name": "Consumer"
  },
  "provider": {
    "name": "Provider"
  },
  "interactions": [
    {
      "description": "A invalid GET request for Date Validation with invalid date parameter",
      "providerState": "There is data",
      "request": {
        "method": "get",
        "path": "/api/provider",
        "query": "validDateTime=lolz"
      },
      "response": {
        "status": 400,
        "headers": {
          "Content-Type": "application/json; charset=utf-8"
        },
        "body": {
          "message": "validDateTime is not a date or time"
        }
      }
    }
  ],
  "metadata": {
    "pactSpecification": {
      "version": "2.0.0"
    }
  }
}
````
This is our contract that specify how we expect the Provider Service API to behave.

Now run ```` git checkout step2 ```` to go to the next step, where we will verify our contract with the provider
