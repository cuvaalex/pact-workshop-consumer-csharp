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
## Create the xUnit Fixture
First we create a new xUnit Fixture class as follow:

````csharp
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
```` 
With this ConsumerPactClassFixture class, we are configuring Pact in the consumer.

When a Pact test is run, Pact will intercept the HTTP requests happening against **localhost:9222** (based on this configuration) and it will return the predefined responses specified in the test. 
Pact will create a contract based on the expectations declared in the tests and the contract will be used in the provider side for its verification.

Create now a xUnit class tests ConsumerPactTests as follow:

````csharp
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

        [Fact]
        public void ItParsesADateCorrectly()
        {
            var expectedDateString = "04/05/2018";
            var expectedDateParsed = DateTime.Parse(expectedDateString).ToString("dd-MM-yyyy HH:mm:ss");

            // Arrange
            _mockProviderService.Given("There is data")
                                .UponReceiving("A valid GET request for Date Validation")
                                .With(new ProviderServiceRequest 
                                {
                                    Method = HttpVerb.Get,
                                    Path = "/api/provider",
                                    Query = $"validDateTime={expectedDateString}"
                                })
                                .WillRespondWith(new ProviderServiceResponse {
                                    Status = 200,
                                    Headers = new Dictionary<string, object>
                                    {
                                        { "Content-Type", "application/json; charset=utf-8" }
                                    },
                                    Body = new 
                                    {
                                        test = "NO",
                                        validDateTime = expectedDateParsed
                                    }
                                });

            // Act
            var result = ConsumerApiClient.ValidateDateTimeUsingProviderApi(expectedDateString, _mockProviderServiceBaseUri).GetAwaiter().GetResult();
            var resultBody = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            // Assert
            Assert.Contains(expectedDateParsed, resultBody);
        }
    }

````

For this first Pact test, we just look that he parse correctly the dates

Notice on the **Arrange** part we explain the request and the answer expected

You can now launch your pact test as follow: ```` dotnet test ````

You should get the following results:

````
Starting test execution, please wait...

A total of 1 test files matched the specified pattern.
INFO  WEBrick 1.3.1
INFO  ruby 2.2.2 (2015-04-13) [x86_64-darwin13]
INFO  WEBrick::HTTPServer#start: pid=23138 port=9222
                                                                                                                                   
Test Run Successful.
Total tests: 2
     Passed: 2
Total time: 7.7799 Seconds

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
      "description": "A valid GET request for Date Validation",
      "providerState": "There is data",
      "request": {
        "method": "get",
        "path": "/api/provider",
        "query": "validDateTime=04/05/2018"
      },
      "response": {
        "status": 200,
        "headers": {
          "Content-Type": "application/json; charset=utf-8"
        },
        "body": {
          "test": "NO",
          "validDateTime": "05-04-2018 00:00:00"
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

Now run ```` git checkout step2 ```` to go to the next step
