using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Xml.Linq;
using System.Xml.Serialization;
using RestSharp;
using Newtonsoft.Json;
using NLog;
using MIC_AMZ_API.App_Code;
using System.Net.Mail;

using Amazon;
using Amazon.Runtime;
using Amazon.SellingPartnerAPIAA;
using System.Runtime.Remoting.Messaging;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace MIC_AMZ_API.Controllers
{
    [RoutePrefix("api")]
    public class SellingPartnerController : ApiController
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        [Obsolete]
        SellingPartnerController()
        {
            try
            {
                var xDocument = XDocument.Load(HostingEnvironment.MapPath(@"~/App_Data/Config.xml"));

                var serializer = new XmlSerializer(typeof(Config));
                using (var reader = new StringReader(xDocument.ToString()))
                {
                    var config = (Config)serializer.Deserialize(reader);

                    mailAlerts = config.MailAlerts;

                    var awsCredentials = config.ConfigureCredentials.AWSCredentials;
                    var lwaCredentials = config.ConfigureCredentials.LWACredentials;

                    var assumeRoleCredentialsTask = GetAssumeRoleCredentials(awsCredentials.AccessKey, awsCredentials.SecretKey, awsCredentials.SessionDuration,
                        awsCredentials.RoleArn, awsCredentials.Policy, awsCredentials.RoleSessionName);

                    var accessTokenResponseTask = GetAccessToken(lwaCredentials.GrantType, lwaCredentials.ClienID,
                        lwaCredentials.ClientSecret, lwaCredentials.RefreshToken);

                    Task.WaitAll(assumeRoleCredentialsTask, accessTokenResponseTask);

                    AssumeRoleCredentials = assumeRoleCredentialsTask.Result;
                    OAuth = accessTokenResponseTask.Result;

                    if (mailAlerts.Enabled)
                    {
                        mailHelper = new MailHelper(config.MailAlerts.ConfigureSMTP);
                    }
                }
            }
            catch (Exception e)
            {
                if (mailHelper != null && mailAlerts != null)
                {
                    mailHelper.SendEmail(mailAlerts.MailAddresses, string.Format("Error Service Config | MIC AMZ"), e.Message, e.StackTrace, null);
                }

                logger.Error(e);
            }
        }

        #region Security Functions

        /*
         * Returns a set of temporary security credentials that you can use to access AWS resources that you might not normally have access to. 
         * These temporary credentials consist of an access key ID, a secret access key, and a security token. Typically, 
         * you use AssumeRole within your account or for cross-account access.
         */
        [Obsolete]
        private Task<ImmutableCredentials> GetAssumeRoleCredentials(string accessKey, string secretKey, int duration, string roleArn, string policy, string roleSessionName)
        {
            var basicAWSCredentials = new BasicAWSCredentials(accessKey, secretKey);
            var amazonSecurityTokenServiceClient = new AmazonSecurityTokenServiceClient(basicAWSCredentials);
            var assumeRoleWithSAMLRequest = new AssumeRoleRequest
            {
                DurationSeconds = duration,
                Policy = policy,
                RoleArn = roleArn,
                RoleSessionName = roleSessionName
            };

            return new AssumeRoleAWSCredentials(amazonSecurityTokenServiceClient, assumeRoleWithSAMLRequest).GetCredentialsAsync();
        }

        /*
         * Exchange an LWA authorization code for an LWA refresh token
         */
        private Task<OAuth.Response> GetAccessToken(string grantType, string clientID, string clientSecret, string refreshToken)
        {
            var accessTokenRequest = new OAuth.Request
            {
                GrantType = grantType,
                ClientID = clientID,
                ClientSecret = clientSecret,
                RefreshToken = refreshToken,
            };

            string resource = "/auth/o2/token";
            var restClient = new RestClient("https://api.amazon.com");
            var restRequest = new RestRequest(resource, Method.POST)
                .AddHeader("Content-Type", "application/json")
                .AddJsonBody(JsonConvert.SerializeObject(accessTokenRequest));

            var response = restClient.ExecuteAsync(restRequest);

            return Task.FromResult(JsonConvert.DeserializeObject<OAuth.Response>(response.Result.Content));
        }

        #endregion


        #region submit feed functios

        /*
        * Workflow for submitting a feed
        */

        [HttpGet]
        [Route ("submit-feed")]
        public string SubmitFeed(string feedType, string marketplaceIds)
        {
            var context = HttpContext.Current;
            try
            {
                CallContext.HostContext = context;
                // Step 0. Get feed data
                string feedData = GetFeedData(feedType);

                if (feedData == null)
                {
                    throw new Exception("Feed data is empty");
                }

                // Step 1. Create a feed document

                var createFeedDocumentResult = CreateFeedDocument();


                // Step 2. Encrypt and upload the feed data

                var key = createFeedDocumentResult.PayLoad.EncryptionDetails.Key;
                var initializationVector = createFeedDocumentResult.PayLoad.EncryptionDetails.InitializationVector;

                var feedDataEncrypt = Cryptography.EncryptStringToBytesAES(feedData, key, initializationVector);
                var url = createFeedDocumentResult.PayLoad.Url;

                var fileUploaded = UploadFile(feedDataEncrypt, url);

                if (fileUploaded)
                {
                    // Step 3. Create a feed

                    var inputFeedDocumentId = createFeedDocumentResult.PayLoad.FeedDocumentId;

                    var createFeedResult = CreateFeed(feedType, marketplaceIds.Split(','), inputFeedDocumentId);


                    // Step 4. Confirm feed processing

                    var feedId = createFeedResult.PayLoad.FeedId;
                    var getFeedResult = GetFeed(feedId);

                    string[] feedStates = { "DONE", "CANCELLED", "FATAL" };

                    var minutes = 1;

                    while (true)
                    {
                        if (!feedStates.Contains(getFeedResult.PayLoad.ProcessingStatus))
                        {
                            if ((minutes++) < 30)
                            {
                                Thread.Sleep(60000);
                            }
                            else
                            {
                                throw new Exception("Wait timeout exceeded");
                            }
                        }
                        else
                        {
                            break;
                        }

                        getFeedResult = GetFeed(feedId);
                    }

                    if (getFeedResult.PayLoad.ProcessingStatus.Equals("DONE"))
                    {
                        // Step 5. Get information for retrieving the feed processing report

                        var feedDocumentId = getFeedResult.PayLoad.ResultFeedDocumentId;

                        var getFeedDocumentResult = GetFeedDocument(feedDocumentId);


                        // Step 6. Download and decrypt the feed processing report

                        key = getFeedDocumentResult.PayLoad.EncryptionDetails.Key;
                        initializationVector = getFeedDocumentResult.PayLoad.EncryptionDetails.InitializationVector;
                        url = getFeedDocumentResult.PayLoad.Url;

                        var dowloadFileResult = DowloadFile(url);

                        var feedDataDencrypt = Cryptography.DecryptStringFromBytesAES(dowloadFileResult, key, initializationVector);

                        try
                        {
                            //File.WriteAllText(@"C:\ProgramData\MICServices\MICAMZ\FeedProcessingReport.xml", feedDataDencrypt);
                        }
                        catch (Exception e)
                        {
                            logger.Error(e);
                        }

                        if (mailHelper != null && mailAlerts != null && mailAlerts.Enabled)
                        {
                            var atachmentList = new List<Attachment>();
                            atachmentList.Add(new Attachment(new MemoryStream(Encoding.UTF8.GetBytes(feedDataDencrypt)), "FeedProcessingReport.xml"));

                            var controller = ViewRenderer.CreateController<GenericController>();
                            mailHelper.SendEmail(mailAlerts.MailAddresses, string.Format("Feed {0} Enviado | MIC AMZ",
                                 Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(feedType.Replace('_', ' ').Trim().ToLower())),
                                    "Informe de procesamiento",
                                    ViewRenderer.RenderViewToString(controller.ControllerContext, "~/Views/Templates/Mail/FeedSubmited.cshtml", null, false), atachmentList);
                        }
                    }
                    else
                    {
                        throw new Exception("Feed state CANCELLED / FATAL");
                    }
                }
                else
                {
                    throw new Exception("Feed content not uploaded");
                }

                return string.Format("Feed {0} Enviado", Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(feedType.Replace('_', ' ').Trim().ToLower()));
            }
            catch (Exception e)
            {
                if (mailHelper != null && mailAlerts != null)
                {
                    mailHelper.SendEmail(mailAlerts.MailAddresses, string.Format("Error Envío De Feed {0} | MIC AMZ",
                        Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(feedType.Replace('_', ' ').Trim().ToLower())), e.Message, e.StackTrace, null);
                }

                logger.Error(e);

                return e.Message;
            }
        }

        private string GetFeedData(string feedType)
        {
            using (var entities = new MICShoppingEntities())
            {
                string feedData = null;
                var controller = ViewRenderer.CreateController<GenericController>();

                switch (feedType)
                {
                    case "POST_INVENTORY_AVAILABILITY_DATA":
                        var feedQty = entities.GetAmazonFeedQty().ToList();
                        feedData = ViewRenderer.RenderViewToString(controller.ControllerContext, "~/Views/Templates/Feed/Inventory.cshtml", feedQty, false);
                        break;
                    case "POST_PRODUCT_PRICING_DATA":
                        var feedPrice = entities.GetAmazonFeedPrice().ToList();
                        feedData = ViewRenderer.RenderViewToString(controller.ControllerContext, "~/Views/Templates/Feed/Price.cshtml", feedPrice, false);
                        break;
                }

                return feedData;
            }
        }

        private CreateFeedDocumentModel.Response CreateFeedDocument()
        {
            var body = new CreateFeedDocumentModel.Request
            {
                ContentType = "text/xml; charset=UTF-8",
            };

            var resource = "/feeds/2020-09-04/documents";
            var restClient = new RestClient("https://sellingpartnerapi-na.amazon.com");
            var restRequest = new RestRequest(resource, Method.POST)
                .AddHeader("x-amz-access-token", OAuth.AccessToken)
                .AddHeader("x-amz-security-token", AssumeRoleCredentials.Token)
                .AddJsonBody(JsonConvert.SerializeObject(body));

            var awsAuthenticationCredentials = new AWSAuthenticationCredentials
            {
                AccessKeyId = AssumeRoleCredentials.AccessKey,
                SecretKey = AssumeRoleCredentials.SecretKey,
                Region = RegionEndpoint.USEast1.SystemName
            };

            restRequest = new AWSSigV4Signer(awsAuthenticationCredentials).Sign(restRequest, restClient.BaseUrl.Host);

            var response = restClient.Execute(restRequest);

            return JsonConvert.DeserializeObject<CreateFeedDocumentModel.Response>(response.Content);
        }

        private static bool UploadFile(byte[] bytes, string url)
        {
            var contentType = "text/xml; charset=UTF-8";

            var restClient = new RestClient(url);
            var restRequest = new RestRequest(Method.PUT)
                .AddParameter(contentType, bytes, ParameterType.RequestBody);

            var response = restClient.Execute(restRequest);

            if (!response.IsSuccessful)
            {
                throw new Exception(response.ErrorMessage);
            }

            return response.IsSuccessful;
        }

        private byte[] DowloadFile(string url)
        {
            var restClient = new RestClient(url);
            var restRequest = new RestRequest(Method.GET);

            var response = restClient.Execute(restRequest);

            return response.RawBytes;
        }

        private FeedDocumentModel.Response CreateFeed(string feedType, string[] marketplaceIds, string inputFeedDocumentId)
        {
            var body = new FeedDocumentModel.Request
            {
                FeedType = feedType,
                MarketplaceIds = marketplaceIds,
                InputFeedDocumentId = inputFeedDocumentId
            };

            var resource = "/feeds/2020-09-04/feeds";
            var restClient = new RestClient("https://sellingpartnerapi-na.amazon.com");
            var restRequest = new RestRequest(resource, Method.POST)
                .AddHeader("x-amz-access-token", OAuth.AccessToken)
                .AddHeader("x-amz-security-token", AssumeRoleCredentials.Token)
                .AddJsonBody(JsonConvert.SerializeObject(body));

            var awsAuthenticationCredentials = new AWSAuthenticationCredentials
            {
                AccessKeyId = AssumeRoleCredentials.AccessKey,
                SecretKey = AssumeRoleCredentials.SecretKey,
                Region = RegionEndpoint.USEast1.SystemName
            };

            restRequest = new AWSSigV4Signer(awsAuthenticationCredentials).Sign(restRequest, restClient.BaseUrl.Host);

            var response = restClient.Execute(restRequest);

            return JsonConvert.DeserializeObject<FeedDocumentModel.Response>(response.Content);
        }

        private GetFeedModel.Response GetFeed(string feedDocumentId)
        {
            var resource = string.Format("/feeds/2020-09-04/feeds/{0}", feedDocumentId);
            var restClient = new RestClient("https://sellingpartnerapi-na.amazon.com");
            var restRequest = new RestRequest(resource, Method.GET)
                .AddHeader("x-amz-access-token", OAuth.AccessToken)
                .AddHeader("x-amz-security-token", AssumeRoleCredentials.Token);

            var awsAuthenticationCredentials = new AWSAuthenticationCredentials
            {
                AccessKeyId = AssumeRoleCredentials.AccessKey,
                SecretKey = AssumeRoleCredentials.SecretKey,
                Region = RegionEndpoint.USEast1.SystemName
            };

            restRequest = new AWSSigV4Signer(awsAuthenticationCredentials).Sign(restRequest, restClient.BaseUrl.Host);

            var response = restClient.Execute(restRequest);

            return JsonConvert.DeserializeObject<GetFeedModel.Response>(response.Content);
        }

        private GetFeedDocumentModel.Response GetFeedDocument(string feedDocumentId)
        {
            var resource = string.Format("/feeds/2020-09-04/documents/{0}", feedDocumentId);
            var restClient = new RestClient("https://sellingpartnerapi-na.amazon.com");
            var restRequest = new RestRequest(resource, Method.GET)
                .AddHeader("x-amz-access-token", OAuth.AccessToken)
                .AddHeader("x-amz-security-token", AssumeRoleCredentials.Token);

            var awsAuthenticationCredentials = new AWSAuthenticationCredentials
            {
                AccessKeyId = AssumeRoleCredentials.AccessKey,
                SecretKey = AssumeRoleCredentials.SecretKey,
                Region = RegionEndpoint.USEast1.SystemName
            };

            restRequest = new AWSSigV4Signer(awsAuthenticationCredentials).Sign(restRequest, restClient.BaseUrl.Host);

            var response = restClient.Execute(restRequest);

            return JsonConvert.DeserializeObject<GetFeedDocumentModel.Response>(response.Content);
        }
        #endregion

        private void GetCatalogItems(string marketPlaceID, string query)
        {
            var resource = "/catalog/v0/items";
            var restClient = new RestClient("https://sellingpartnerapi-na.amazon.com");
            var restRequest = new RestRequest(resource, Method.GET)
                .AddHeader("x-amz-access-token", OAuth.AccessToken)
                .AddHeader("x-amz-security-token", AssumeRoleCredentials.Token)
                .AddQueryParameter("MarketplaceId", marketPlaceID)
                .AddQueryParameter("Query", query);

            var awsAuthenticationCredentials = new AWSAuthenticationCredentials
            {
                AccessKeyId = AssumeRoleCredentials.AccessKey,
                SecretKey = AssumeRoleCredentials.SecretKey,
                Region = RegionEndpoint.USEast1.SystemName
            };

            restRequest = new AWSSigV4Signer(awsAuthenticationCredentials).Sign(restRequest, restClient.BaseUrl.Host);
        }

        private readonly ImmutableCredentials AssumeRoleCredentials;
        private readonly OAuth.Response OAuth;

        private readonly MailHelper mailHelper;
        private readonly MailAlerts mailAlerts;
    }
}
