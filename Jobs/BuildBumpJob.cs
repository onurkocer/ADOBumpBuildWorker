/*
 * This Sample Code is provided for the purpose of illustration only and is not
 * intended to be used in a production environment.
 * THIS SAMPLE CODE AND ANY RELATED INFORMATION ARE PROVIDED "AS IS" WITHOUT 
 * WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
 * TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR 
 * PURPOSE.  
 * We (Microsoft) grant You a nonexclusive, royalty-free right to use and modify the Sample
 * Code and to reproduce and distribute the object code form of the Sample 
 * Code, provided that You agree: 
 *   (i)  to not use Our name, logo, or trademarks to market Your software 
 *        product in which the Sample Code is embedded; 
 *   (ii) to include a valid copyright notice on Your software product in which 
 *        the Sample Code is embedded; and 
 *  (iii) to indemnify, hold harmless, and defend Us and Our suppliers from and 
 *        against any claims or lawsuits, including attorneys’ fees, that arise 
 *        or result from the use or distribution of the Sample Code.
*/


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BumpBuildWorker
{
    /// <summary>
    /// Worker job that is triggered according to cron expression.
    /// Queries Azure DevOps for the jobs in the queue for given agent pools and give priority to manuel or CI triggered jobs in the queue.
    /// </summary>
    public class BuildBumpJob : CronJobService
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly IEnumerable<string> _poolNames;
        private readonly ILogger<BuildBumpJob> _logger;
        private const string AdoHttpClientName = "adoclient";

        public BuildBumpJob(IScheduleConfig<BuildBumpJob> config, 
            ILogger<BuildBumpJob> logger, 
            IHttpClientFactory clientFactory,
            IConfiguration configuration)
            : base(config.CronExpression, config.TimeZoneInfo)
        {
            _logger = logger;
            _clientFactory = clientFactory;
            _poolNames = configuration.GetSection("PoolNames").Get<IEnumerable<string>>();
        }

        /// <summary>
        /// Executes only when the process is up.
        /// </summary>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting...");
            return base.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Triggered in each cron schedule.
        /// </summary>
        public async override Task DoWork(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting to execute job.");

            // Chech If given pools exists and get id values.
            var pools = await GetPoolsFromName(_poolNames);
            if (!pools.Any())
            {
                _logger.LogError("Could not find any pool to execute bumping rules.");
                return;
            }

            foreach (var pool in pools)
            {
                JobRequestResponse jobRequests = null;
                List<JobRequest> listToBump = new List<JobRequest>();
                bool scheduledBuildInQueue = false;

                try
                {
                    var client = _clientFactory.CreateClient(AdoHttpClientName);

                    //get the list of job requests in the queue         
                    HttpResponseMessage response = await client.GetAsync("_apis/distributedtask/pools/" + pool.Id + "/jobrequests?includeStatus=true");

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        jobRequests = JsonSerializer.Deserialize<JobRequestResponse>(responseBody, 
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase});
                    }
                    else
                    {
                        _logger.LogError($"Could not retrive job requests from pool with Id: {pool.Id} Name: {pool.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error occured while getting job requests for Pool with Id: {pool.Id} Name: {pool.Name} Ex: {ex}");
                    // Continue with next job request
                    continue;
                }

                if (jobRequests == null)
                {
                    _logger.LogInformation($"No job requests exist for pool with Id: {pool.Id} Name: {pool.Name}");
                    // Continue with next pool
                    continue;
                }

                for (int i = 0; i < jobRequests.Count; i++)
                {
                    JobRequest jobRequest = jobRequests.Value[i];

                    //active request if null
                    string result = jobRequest.Result;

                    //if False manually triggered or CI triggered
                    string isScheduled = jobRequest.Data.IsScheduledKey;

                    //already running if not null
                    ReservedAgent reservedAgent = jobRequest.ReservedAgent;

                    //waiting request in the queue
                    if (result is null && reservedAgent is null)
                    {
                        if (!string.IsNullOrEmpty(isScheduled) && isScheduled.Equals("False"))
                        {
                            listToBump.Add(jobRequest);
                        }
                        else
                        {
                            scheduledBuildInQueue = true;
                        }
                    }
                }

                //no need to bump any builds if there are no scheduled builds in the queue
                if (scheduledBuildInQueue)
                {
                    _logger.LogTrace($"One or more scheduled builds found in the queue.");
                    //reversing the list to keep the requests in the same order (FIFO) after bumping
                    listToBump.Reverse();
                    await BumpJobs(pool, listToBump);
                }
            }
            _logger.LogInformation("Job is completed.");
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Job is stopping.");
            return base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Calls jobrequests endpoint to bump given list of jobs.
        /// </summary>
        /// <param name="pool">Owner of job requests</param>
        /// <param name="listToBump">List of job requests to prioritize</param>
        /// <returns></returns>
        private async Task BumpJobs(Pool pool, List<JobRequest> listToBump)
        {
            int count = 0;
            foreach (JobRequest jobRequest in listToBump)
            {
                _logger.LogTrace($"Bumping job request with id {jobRequest.RequestId}");

                //bump the build to the top of the queue
                string jobId = jobRequest.RequestId.ToString();
                string url = "_apis/distributedtask/pools/" + pool.Id + "/jobrequests/" + jobId + "?lockToken=00000000-0000-0000-0000-000000000000&updateOptions=1&api-version=6.1-preview.1";
                string jsonContent = "{" + "\"requestId\":" + jobId + "}";

                try
                {
                    var client = _clientFactory.CreateClient(AdoHttpClientName);
                    var method = new HttpMethod("PATCH");
                    var request = new HttpRequestMessage(method, url)
                    {
                        Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                    };

                    HttpResponseMessage response = new HttpResponseMessage();
                    response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        count += 1;
                        _logger.LogTrace($"Job with id:{jobId} in pool with id: {pool.Id} and pool name: {pool.Name} bumped successfully");
                    }
                    else
                    {
                        string responseBody = await response.Content?.ReadAsStringAsync();
                        _logger.LogTrace($"Bump request failed. Response: {responseBody}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error occured while bumping job with Id: {jobId} Ex: {ex}");
                    // Continue with next job request
                    continue;
                }
            }
            _logger.LogInformation($"Total of {count} jobs bumped successfully.");
        }

        /// <summary>
        /// Query Azure DevOps to get pool Ids from given pool names.
        /// </summary>
        /// <param name="poolNames">Pools to query for id values.</param>
        /// <returns>Returns list of pool object that contains pool id and names value</returns>
        private async Task<IEnumerable<Pool>> GetPoolsFromName(IEnumerable<string> poolNames)
        {
            var result = new List<Pool>();

            try
            {
                var client = _clientFactory.CreateClient(AdoHttpClientName);
                HttpResponseMessage response = await client.GetAsync("_apis/distributedtask/pools/");

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    PoolResponse poolResponse = JsonSerializer.Deserialize<PoolResponse>(responseBody,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    var targetPools = poolResponse.Value.Where(p => poolNames.Contains(p.Name));
                    result.AddRange(targetPools);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not get pool information response. Ex: {ex}");
            }

            return result.AsEnumerable();
        }
    }
}
