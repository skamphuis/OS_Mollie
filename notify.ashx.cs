using System;
using System.Runtime.Remoting.Contexts;
using System.Threading.Tasks;
using System.Web;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Log.EventLog;
using Mollie.Api.Client;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models.Payment.Response;
using NBrightCore.common;
using Nevoweb.DNN.NBrightBuy.Components;

namespace OS_Mollie.DNN.NBrightStore
{
    /// <summary>
    /// Summary description for XMLconnector
    /// </summary>
    public class OS_MollieNotify : IHttpHandler
    {
        /// <summary>
        /// This function needs to process and returned message from the bank.
        /// This processing may vary widely between banks.
        /// </summary>
        /// <param name="context"></param>
        public void ProcessRequest(HttpContext context)
        {
            var info = ProviderUtils.GetProviderSettings();

            var objEventLog = new EventLogController();
            PortalSettings portalsettings = new PortalSettings();

            try
            {
                var debugMode = info.GetXmlPropertyBool("genxml/checkbox/debugmode");
                var debugMsg = "START CALL" + DateTime.Now.ToString("s") + " </br>";
                var rtnMsg = "version=2" + Environment.NewLine + "cdr=1";


                var providerSettings = ProviderUtils.GetProviderSettings();
                var ApiKey = providerSettings.GetXmlProperty("genxml/textbox/key");
                var orderid = Utils.RequestQueryStringParam(context, "orderid");

                debugMsg += "orderid: " + orderid + "</br>";


                if (Utils.IsNumeric(orderid))
                {

                    var orderData = new OrderData(Convert.ToInt32(orderid));

                    IPaymentClient paymentClient = new PaymentClient(ApiKey);
                    var task = Task.Run(async () => await paymentClient.GetPaymentAsync(orderData.PaymentPassKey));
                    task.Wait();
                    PaymentResponse paymentClientResult = task.Result;

                    objEventLog.AddLog("Mollie Webhook call for orderid: " + orderid  , "Status: " + paymentClientResult.Status, portalsettings, -1, EventLogController.EventLogType.ADMIN_ALERT);

                    //Waiting for Payment 060
                    //Payment OK 040
                    //Incomplete 010
                    //Cancelled 030

                    switch (paymentClientResult.Status.ToString().ToLower())
                    {
                        case "paid":
                            orderData.PaymentOk("040", true);
                            rtnMsg = "OK";
                            break;
                        case "failed":
                            orderData.PaymentFail("010");
                            rtnMsg = "OK";
                            break;
                        case "canceled":
                            orderData.PaymentFail("030");
                            rtnMsg = "OK";
                            break;
                        case "expired":
                            orderData.PaymentFail("010");
                            rtnMsg = "OK";
                            break;
                        default:
                            orderData.PaymentFail("010");
                            rtnMsg = "OK";
                            break;
                    }
                }
                if (debugMode)
                {
                    debugMsg += "Return Message: " + rtnMsg;
                    info.SetXmlProperty("genxml/debugmsg", debugMsg);
                    var modCtrl = new NBrightBuyController();
                    modCtrl.Update(info);
                }

                HttpContext.Current.Response.Clear();
                HttpContext.Current.Response.Write(rtnMsg);
                HttpContext.Current.Response.ContentType = "text/plain";
                HttpContext.Current.Response.CacheControl = "no-cache";
                HttpContext.Current.Response.Expires = -1;
                HttpContext.Current.Response.End();
            }
            catch (Exception ex)
            {
                objEventLog.AddLog("Mollie Webhook call failed" , ex.Message + " " + ex.InnerException , portalsettings, -1, EventLogController.EventLogType.ADMIN_ALERT);

                if (!ex.ToString().StartsWith("System.Threading.ThreadAbortException")) // we expect a thread abort from the End response.
                {
                    info.SetXmlProperty("genxml/debugmsg", "OS_Mollie ERROR: " + ex.ToString());
                    var modCtrl = new NBrightBuyController();
                    modCtrl.Update(info);
                }
            }
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }


    }
}