using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SampleSkills
{
    /// <summary>
    /// Sample custom skill that wraps the Bing custom search API to connect it with a 
    /// AI enrichment pipeline.
    /// </summary>
    public static class BingCustomSearch
    {
        #region Credentials
        // IMPORTANT: Make sure to enter your credential and to verify the API endpoint matches yours.
       // static readonly string bingApiEndpoint = "https://api.cognitive.microsoft.com/bingcustomsearch/v7.0/search";
        static readonly string bingApiEndpoint = "https://fraudsearch2.cognitiveservices.azure.com/bingcustomsearch/v7.0/search";
      //  static readonly string key = "3cc45cd1c4e046fd9da53442a06d05a0";
        static readonly string key = "a5e10cfe802e4a62a715ad88fcfadc85";
        static readonly string customconfig = "ea888480-1245-4f7d-a302-98c06bc5190f";
        #endregion

        #region Class used to deserialize the request
        private class InputRecord
        {
            public class InputRecordData
            {
                public string Name { get; set; }
            }

            public string RecordId { get; set; }
            public InputRecordData Data { get; set; }
        }

        private class WebApiRequest
        {
            public List<InputRecord> Values { get; set; }
        }
        #endregion

        #region Classes used to serialize the response

        public class OutputRecord
        {
         
            public class OutputRecordData
            {
                public List<BingCustomSearchEntities> DataEntities { get; set; }

                public class BingCustomSearchEntities
                {
                    public string name { get; set; }

                    public string url { get; set; }

                    public string displayUrl { get; set; }

                    public string snippet { get; set; }

                    public DateTime dateLastCrawled { get; set; }


                }
            }

            public class OutputRecordMessage
            {
                public string Message { get; set; }
            }

            public string RecordId { get; set; }
            public OutputRecordData Data { get; set; }
            public List<OutputRecordMessage> Errors { get; set; }
            public List<OutputRecordMessage> Warnings { get; set; }


        }

        private class WebApiResponse
        {
            public List<OutputRecord> Values { get; set; }
        }
        #endregion


        // <responseClasses>

        public class BingCustomSearchResponse

        {

            public string _type { get; set; }

            public WebPages webPages { get; set; }

        }

         public class WebPages

        {

            public string webSearchUrl { get; set; }

            public int totalEstimatedMatches { get; set; }

            public WebPage[] value { get; set; }

        }



        public class WebPage

        {

            public string name { get; set; }

            public string url { get; set; }

            public string displayUrl { get; set; }

            public string snippet { get; set; }

            public DateTime dateLastCrawled { get; set; }

           

        }



      

      
        #region The Azure Function definition

        [FunctionName("OnlineFraudSearch")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Custom Search function: C# HTTP trigger function processed a request.");

            var response = new WebApiResponse
            {
                Values = new List<OutputRecord>()
            };

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            var data = JsonConvert.DeserializeObject<WebApiRequest>(requestBody);
            log.LogInformation("Custom Search function: Request is validated." + data);
            // Do some schema validation
            if (data == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema.");
            }
            if (data.Values == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema. Could not find values array.");
            }
            log.LogInformation("Custom Search function: Starting the record .");
            // Calculate the response for each value.
            foreach (var record in data.Values)
            {
                if (record == null || record.RecordId == null) continue;

                OutputRecord responseRecord = new OutputRecord
                {
                    RecordId = record.RecordId
                };

                try
                {
                    responseRecord.Data = GetEntityMetadata(record.Data.Name,log).Result;
                    log.LogInformation("Custom Search function: Response record." + responseRecord.Data.ToString());
                }
                catch (Exception e)
                {
                    // Something bad happened, log the issue.
                    var error = new OutputRecord.OutputRecordMessage
                    {
                        Message = e.Message
                    };

                    responseRecord.Errors = new List<OutputRecord.OutputRecordMessage>
                    {
                        error
                    };

                    log.LogError("Custom Search function: Error message." + error.Message );
                    
                }
                finally
                {
                    response.Values.Add(responseRecord);
                }
            }

            return (ActionResult)new OkObjectResult(response);
        }

        #endregion

        #region Methods to call the Bing API
        /// <summary>
        /// Gets metadata for a particular entity based on its name using Bing Entity Search
        /// </summary>
        /// <param name="entityName">The name of the entity to extract data for.</param>
        /// <returns>Asynchronous task that returns entity data. </returns>
        public async static Task<OutputRecord.OutputRecordData> GetEntityMetadata(string entityName,ILogger log)
        {
            var uri = bingApiEndpoint + "?q=" + entityName + " Fraud" + "&customconfig="+ customconfig + "&mkt=en-us&count=10&offset=0&safesearch=Moderate";
            var result = new OutputRecord.OutputRecordData();

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(uri)
            })
            {
                request.Headers.Add("Ocp-Apim-Subscription-Key", key);

                HttpResponseMessage responseMsg = await client.SendAsync(request);
                string responseBody = await responseMsg?.Content?.ReadAsStringAsync();
                BingCustomSearchResponse bingResult = JsonConvert.DeserializeObject<BingCustomSearchResponse>(responseBody);
                 log.LogInformation("Custom Search function: Response result." + bingResult.ToString());
                // BingResponse bingResult = JsonConvert.DeserializeObject<BingResponse>(responseBody);
                if (bingResult != null)
                {
                    // In addition to the list of entities that could match the name, for simplicity let's return information
                    // for the top match as additional metadata at the root object.
                    // return AddTopEntityMetadata(bingResult.Entities?.Value);

                    for (int i = 0; i < bingResult.webPages.value.Length; i++)

                    {

                        var webPage = bingResult.webPages.value[i];



                        Console.WriteLine("name: " + webPage.name);

                        Console.WriteLine("url: " + webPage.url);

                        Console.WriteLine("displayUrl: " + webPage.displayUrl);

                        Console.WriteLine("snippet: " + webPage.snippet);

                        Console.WriteLine("dateLastCrawled: " + webPage.dateLastCrawled);

                        Console.WriteLine();

                    }

                    return AddTopEntityMetadata(bingResult.webPages?.value);

                }
            }

            return result;
        }


        #endregion
        
        #region Methods to populate output record
        public static OutputRecord.OutputRecordData AddTopEntityMetadata(WebPage[] entities)
        {
            List<OutputRecord.OutputRecordData.BingCustomSearchEntities>  my = new List<OutputRecord.OutputRecordData.BingCustomSearchEntities>();

            OutputRecord.OutputRecordData test = new OutputRecord.OutputRecordData();
            
            if (entities != null)
            {
               
                foreach (WebPage entity in entities)
                {


                    OutputRecord.OutputRecordData.BingCustomSearchEntities ex = new OutputRecord.OutputRecordData.BingCustomSearchEntities();

                    ex.snippet = entity.snippet;
                    ex.displayUrl = entity.displayUrl;
                    ex.name = entity.name;

                    my.Add(ex);

                   
                    //my.DataEntities.Add(ex);
                   // return rootObject;
                }

            }
            test.DataEntities = my;
            return test;
        }
#endregion
    
    }
}