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


using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BumpBuildWorker
{
    public class Self
    {
        public string Href { get; set; }
    }

    public class Web
    {
        public string Href { get; set; }
    }

    public class Links
    {
        public Self Self { get; set; }
        public Web Web { get; set; }
    }

    public class ReservedAgent
    {
        [JsonPropertyName("_links")]
        public Links _links { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string OsDescription { get; set; }
        public bool Enabled { get; set; }
        public string Status { get; set; }
        public string ProvisioningState { get; set; }
        public string AccessPoint { get; set; }
    }

    public class Web2
    {
        public string Href { get; set; }
    }

    public class Self2
    {
        public string Href { get; set; }
    }

    public class Links2
    {
        public Web2 Web { get; set; }
        public Self2 Self { get; set; }
    }

    public class Definition
    {
        [JsonPropertyName("_links")]
        public Links2 _links { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Web3
    {
        public string Href { get; set; }
    }

    public class Self3
    {
        public string Href { get; set; }
    }

    public class Links3
    {
        public Web3 Web { get; set; }
        public Self3 Self { get; set; }
    }

    public class Owner
    {
        [JsonPropertyName("_links")]
        public Links3 _links { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Data
    {
        public string ParallelismTag { get; set; }
        [JsonPropertyName("IsScheduledKey")]
        public string IsScheduledKey { get; set; }
    }

    public class AgentSpecification
    {
        [JsonPropertyName("VMImage")]
        public string VMImage { get; set; }
    }

    public class JobRequest
    {
        public int RequestId { get; set; }
        public DateTime QueueTime { get; set; }
        public DateTime AssignTime { get; set; }
        public DateTime ReceiveTime { get; set; }
        public DateTime FinishTime { get; set; }
        public string Result { get; set; }
        public string ServiceOwner { get; set; }
        public string HostId { get; set; }
        public string ScopeId { get; set; }
        public string PlanType { get; set; }
        public string PlanId { get; set; }
        public string JobId { get; set; }
        public List<string> Demands { get; set; }
        public ReservedAgent ReservedAgent { get; set; }
        public Definition Definition { get; set; }
        public Owner Owner { get; set; }
        public Data Data { get; set; }
        public int PoolId { get; set; }
        public AgentSpecification AgentSpecification { get; set; }
        public string OrchestrationId { get; set; }
        public bool MatchesAllAgentsInPool { get; set; }
        public int Priority { get; set; }
    }

    public class JobRequestResponse
    {
        public int Count { get; set; }
        public List<JobRequest> Value { get; set; }
    }
}
