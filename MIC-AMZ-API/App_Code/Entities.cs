using System;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace MIC_AMZ_API.App_Code
{
    [Serializable]
    [XmlRoot("Config")]
    public class Config
    {
        [XmlElement("MailAlerts")]
        public MailAlerts MailAlerts { get; set; }        
        
        [XmlElement("ConfigureCredentials")]
        public ConfigureCredentials ConfigureCredentials { get; set; }
    }

    public class MailAlerts
    {
        [XmlElement("Enabled")]
        public bool Enabled { get; set; }

        [XmlElement("ConfigureSMTP")]
        public ConfigureSMTP ConfigureSMTP { get; set; }

        [XmlElement("MailAddresses")]
        public string MailAddresses { get; set; }
    }

    public class ConfigureSMTP
    {
        [XmlElement("MailAddress")]
        public string MailAddress { get; set; }        
        
        [XmlElement("Host")]
        public string Host { get; set; }

        [XmlElement("Port")]
        public int Port { get; set; }

        [XmlElement("Credentials")]
        public NetworkCredential Credentials { get; set; }

        public class NetworkCredential
        {
            [XmlElement("UserName")]
            public string UserName { get; set; }

            [XmlElement("Password")]
            public string Password { get; set; }
        }
    }

    public class ConfigureCredentials
    {
        [XmlElement("AWSCredentials")]
        public AWSCredentials AWSCredentials { get; set; }

        [XmlElement("LWACredentials")]
        public LWACredentials LWACredentials { get; set; }
    }

    [Serializable]
    public class AWSCredentials
    {
        [XmlElement("AccessKey")]
        public string AccessKey { get; set; }

        [XmlElement("SecretKey")]
        public string SecretKey { get; set; }

        [XmlElement("Region")]
        public string Region { get; set; }

        [XmlElement("UserArn")]
        public string UserArn { get; set; }

        [XmlElement("RoleArn")]
        public string RoleArn { get; set; }

        [XmlElement("RoleSessionName")]
        public string RoleSessionName { get; set; }

        [XmlElement("Policy")]
        public string Policy { get; set; }

        [XmlElement("SessionDuration")]
        public int SessionDuration { get; set; }
    }

    [Serializable]
    public class LWACredentials
    {
        [XmlElement("GrantType")]
        public string GrantType { get; set; }

        [XmlElement("ClienID")]
        public string ClienID { get; set; }

        [XmlElement("ClientSecret")]
        public string ClientSecret { get; set; }

        [XmlElement("RefreshToken")]
        public string RefreshToken { get; set; }
    }

    public class OAuth
    {
        public class Request
        {
            [JsonProperty(PropertyName = "grant_type")]
            public string GrantType { get; set; }

            [JsonProperty(PropertyName = "refresh_token")]
            public string RefreshToken { get; set; }

            [JsonProperty(PropertyName = "client_id")]
            public string ClientID { get; set; }

            [JsonProperty(PropertyName = "client_secret")]
            public string ClientSecret { get; set; }
        }

        public class Response
        {
            [JsonProperty(PropertyName = "access_token")]
            public string AccessToken { get; set; }

            [JsonProperty(PropertyName = "refresh_token")]
            public string RefreshToken { get; set; }

            [JsonProperty(PropertyName = "token_type")]
            public string TokenType { get; set; }

            [JsonProperty(PropertyName = "expires_in")]
            public int ExpiresIn { get; set; }
        }
    }

    public class CreateFeedDocumentModel
    {
        public class Request
        {
            [JsonProperty(PropertyName = "contentType")]
            public string ContentType { get; set; }
        }

        public class Response
        {
            [JsonProperty(PropertyName = "payload")]
            public PayLoadResult PayLoad { get; set; }

            public class PayLoadResult
            {

                [JsonProperty(PropertyName = "encryptionDetails")]
                public EncryptionDetailsResult EncryptionDetails { get; set; }

                [JsonProperty(PropertyName = "feedDocumentId")]
                public string FeedDocumentId { get; set; }

                [JsonProperty(PropertyName = "url")]
                public string Url { get; set; }

                public class EncryptionDetailsResult
                {
                    [JsonProperty(PropertyName = "standard")]
                    public string Standard { get; set; }

                    [JsonProperty(PropertyName = "initializationVector")]
                    public string InitializationVector { get; set; }

                    [JsonProperty(PropertyName = "key")]
                    public string Key { get; set; }
                }
            }
        }
    }

    public class FeedDocumentModel
    {
        public class Request
        {
            [JsonProperty(PropertyName = "feedType")]
            public string FeedType { get; set; }

            [JsonProperty(PropertyName = "marketplaceIds")]
            public string[] MarketplaceIds { get; set; }

            [JsonProperty(PropertyName = "inputFeedDocumentId")]
            public string InputFeedDocumentId { get; set; }
        }

        public class Response
        {
            [JsonProperty(PropertyName = "payload")]
            public PayLoadResult PayLoad { get; set; }

            public class PayLoadResult
            {
                [JsonProperty(PropertyName = "feedId")]
                public string FeedId { get; set; }
            }
        }
    }

    public class GetFeedModel
    {
        public class Response
        {
            [JsonProperty(PropertyName = "payload")]
            public PayLoadResult PayLoad { get; set; }

            public class PayLoadResult
            {
                [JsonProperty(PropertyName = "processingStatus")]
                public string ProcessingStatus { get; set; }

                [JsonProperty(PropertyName = "marketplaceIds")]
                public string[] MarketplaceIds { get; set; }

                [JsonProperty(PropertyName = "feedId")]
                public string FeedId { get; set; }

                [JsonProperty(PropertyName = "feedType")]
                public string FeedType { get; set; }

                [JsonProperty(PropertyName = "createdTime")]
                public DateTime CreatedTime { get; set; }

                [JsonProperty(PropertyName = "processingStartTime")]
                public DateTime ProcessingStartTime { get; set; }

                [JsonProperty(PropertyName = "resultFeedDocumentId")]
                public string ResultFeedDocumentId { get; set; }
            }
        }
    }

    public class GetFeedDocumentModel
    {
        public class Response
        {
            [JsonProperty(PropertyName = "payload")]
            public PayLoadResult PayLoad { get; set; }

            public class PayLoadResult
            {
                [JsonProperty(PropertyName = "feedDocumentId")]
                public string FeedDocumentId { get; set; }

                [JsonProperty(PropertyName = "url")]
                public string Url { get; set; }

                [JsonProperty(PropertyName = "encryptionDetails")]
                public EncryptionDetailsResult EncryptionDetails { get; set; }

                public class EncryptionDetailsResult
                {
                    [JsonProperty(PropertyName = "standard")]
                    public string Standard { get; set; }

                    [JsonProperty(PropertyName = "initializationVector")]
                    public string InitializationVector { get; set; }

                    [JsonProperty(PropertyName = "key")]
                    public string Key { get; set; }
                }
            }
        }
    }
}