using System;
using System.Collections.Generic;
using Consumer;
using PactNet.Mocks.MockHttpService;
using PactNet.Mocks.MockHttpService.Models;
using Xunit;

namespace consumer.tests
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
            Assert.Contains(expectedDateParsed, (string) resultBody);
        }
    }
}