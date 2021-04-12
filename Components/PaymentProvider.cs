using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Services.Log.EventLog;
using Mollie.Api.Client;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models;
using Mollie.Api.Models.Payment;
using Mollie.Api.Models.Payment.Request;
using Mollie.Api.Models.Payment.Response;
using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;

namespace OS_Mollie
{
    public class OS_MolliePaymentProvider : Nevoweb.DNN.NBrightBuy.Components.Interfaces.PaymentsInterface
    {
        public override string Paymentskey { get; set; }

        #region GetTemplate
        public override string GetTemplate(NBrightInfo cartInfo)
        {
            var info = ProviderUtils.GetProviderSettings();
            var templateName = info.GetXmlProperty("genxml/textbox/checkouttemplate");
            var passSettings = info.ToDictionary();
            foreach (var s in StoreSettings.Current.Settings()) // copy store setting, otherwise we get a byRef assignement
            {
                if (passSettings.ContainsKey(s.Key))
                {
                    passSettings[s.Key] = s.Value;
                }
                else
                {
                    passSettings.Add(s.Key, s.Value);
                }
            }

            return NBrightBuyUtils.RazorTemplRender(templateName, 0, "", info, "/DesktopModules/NBright/OS_Mollie", "config", Utils.GetCurrentCulture(), passSettings); ;
        }
        #endregion

        #region RedirectForPayment
        public override string RedirectForPayment(OrderData orderData)
        {
            try
            {
                var appliedtotal = orderData.PurchaseInfo.GetXmlPropertyDouble("genxml/appliedtotal");
                var alreadypaid = orderData.PurchaseInfo.GetXmlPropertyDouble("genxml/alreadypaid");

                var info = ProviderUtils.GetProviderSettings();

                string cartDesc = info.GetXmlProperty("genxml/textbox/cartdescription");
                var ApiKey = info.GetXmlProperty("genxml/textbox/key");
                var idealOnly = true; // default to previous behaviour
                if (info.XMLDoc.SelectSingleNode("genxml/checkbox/idealonly") != null)
                {
                    // take the value from the settings, if it exists
                    idealOnly = info.GetXmlPropertyBool("genxml/checkbox/idealonly");
                }
                var notifyUrl = Utils.ToAbsoluteUrl("/DesktopModules/NBright/OS_Mollie/notify.ashx");
                var returnUrl = Globals.NavigateURL(StoreSettings.Current.PaymentTabId, "");

                //orderid/itemid
                var ItemId = orderData.PurchaseInfo.ItemID.ToString("");

                var productOrderNumber = orderData.OrderNumber;

                ////var nbi = new NBrightInfo();
                //nbi.XMLData = orderData.payselectionXml;
                //var paymentMethod = nbi.GetXmlProperty("genxml/textbox/paymentmethod");
                //var paymentBank = nbi.GetXmlProperty("genxml/textbox/paymentbank");
                //var apiKey = liveApiKey;

                string specifier;
                CultureInfo culture;
                specifier = "F";
                culture = CultureInfo.CreateSpecificCulture("en-US");
                var totalString = decimal.Parse((appliedtotal - alreadypaid).ToString("0.00"));
                var totalPrijsString2 = totalString.ToString(specifier, culture).Replace('€', ' ').Trim();

                PaymentResponse result = null;
                if (idealOnly)
                {
                    IPaymentClient paymentClient = new PaymentClient(ApiKey);
                    IdealPaymentRequest paymentRequestIdeal = new IdealPaymentRequest()
                    {
                        Amount = new Amount(Currency.EUR, totalPrijsString2),
                        Description = "Bestelling ID: " + " " + productOrderNumber + " " + cartDesc,
                        RedirectUrl = returnUrl + "/orderid/" + ItemId,
                        WebhookUrl = notifyUrl + "?orderid=" + ItemId,
                        Locale = Locale.nl_NL,
                        Method = PaymentMethod.Ideal
                    };

                    var task = Task.Run(async () => (IdealPaymentResponse)await paymentClient.CreatePaymentAsync(paymentRequestIdeal));
                    task.Wait();
                    result = task.Result;
                }
                else
                {
                    IPaymentClient paymentClient = new PaymentClient(ApiKey);
                    PaymentRequest paymentRequest = new PaymentRequest()
                    {
                        Amount = new Amount(Currency.EUR, totalPrijsString2),
                        Description = "Bestelling ID: " + " " + productOrderNumber + " " + cartDesc,
                        RedirectUrl = returnUrl + "/orderid/" + ItemId,
                        WebhookUrl = notifyUrl + "?orderid=" + ItemId,
                        Locale = Locale.nl_NL,
                    };

                    var task = Task.Run(async () => await paymentClient.CreatePaymentAsync(paymentRequest));
                    task.Wait();
                    result = task.Result;
                }

                orderData.PaymentPassKey = result.Id;
                orderData.OrderStatus = "020";
                orderData.PurchaseInfo.SetXmlProperty("genxml/paymenterror", "");
                orderData.PurchaseInfo.SetXmlProperty("genxml/posturl", result.Links.Checkout.Href);
                orderData.PurchaseInfo.Lang = Utils.GetCurrentCulture();
                orderData.SavePurchaseData();

                try
                {
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                   // objEventLog.AddLog("Betaling aangemaakt: ", "ID: " + result.Id + " Bedrag:" + result.Amount.Currency + " ItemId: " + ItemId, PortalSettings, -1, EventLogController.EventLogType.ADMIN_ALERT);

                    HttpContext.Current.Response.Clear();
                    //redirect naar Mollie
                    HttpContext.Current.Response.Redirect(result.Links.Checkout.Href);
                }
                catch (Exception ex)
                {
                    // rollback transaction
                    orderData.PurchaseInfo.SetXmlProperty("genxml/paymenterror", "<div>ERROR: Invalid payment data </div><div>" + ex + "</div>");
                    orderData.PaymentFail("010");
                    //objEventLog.AddLog("Betaling aanmaken mislukt: ", "ID: " + result.Id + " Bedrag:" + result.Amount.Currency + " ItemId: " + ItemId, PortalSettings, -1, EventLogController.EventLogType.ADMIN_ALERT);

                    var param = new string[3];
                    param[0] = "orderid=" + orderData.PurchaseInfo.ItemID.ToString("");
                    param[1] = "status=0";
                    return Globals.NavigateURL(StoreSettings.Current.PaymentTabId, "", param);
                }
                try
                {
                    HttpContext.Current.Response.End();
                }
                catch (Exception ex)
                {
                    // this try/catch to avoid sending error 'ThreadAbortException'  
                }

                return "";
            }
            catch (Exception ex)
            {
                //objEventLog.AddLog("Foutje", "Dus:" + ex.Message + " " + ex.InnerException , PortalSettings, -1, EventLogController.EventLogType.ADMIN_ALERT);
                return ex.InnerException.ToString();
            }
        }
        #endregion

        #region ProcessPaymentReturn
        public override string ProcessPaymentReturn(HttpContext context)
        {
            var info = ProviderUtils.GetProviderSettings();
            var ApiKey = info.GetXmlProperty("genxml/textbox/key");
            var orderid = Utils.RequestQueryStringParam(context, "orderid");

            var objEventLog = new EventLogController();
            PortalSettings portalsettings = new PortalSettings();

            var orderData = new OrderData(Convert.ToInt32(orderid));
            var rtnerr = orderData.PurchaseInfo.GetXmlProperty("genxml/paymenterror");

            if (Utils.IsNumeric(orderid))
            {
                IPaymentClient paymentClient = new PaymentClient(ApiKey);
                var task = Task.Run(async () => await paymentClient.GetPaymentAsync(orderData.PaymentPassKey));
                task.Wait();
                PaymentResponse paymentClientResult = task.Result;

                objEventLog.AddLog("Mollie ProcessPaymentReturn", "Status: " + paymentClientResult.Status.Value.ToString() + " OrderId:" + orderid + " Mollie Id: " + orderData.PaymentPassKey, portalsettings, -1, EventLogController.EventLogType.ADMIN_ALERT);

                switch (paymentClientResult.Status.ToString().ToLower())
                {
                    case "open":

                        //waiting for bank?
                        rtnerr = orderData.PurchaseInfo.GetXmlProperty("genxml/paymenterror");
                        rtnerr = "Mollie open"; // to return this so a fail is activated.
                        return GetReturnTemplate(orderData, false, "Mollie Open");

                    case "paid":

                        return GetReturnTemplate(orderData, true, "");

                    case "failed":

                        rtnerr = orderData.PurchaseInfo.GetXmlProperty("genxml/paymenterror");
                        rtnerr = "Mollie failed"; // to return this so a fail is activated.
                        return GetReturnTemplate(orderData, false, "Mollie failed");

                    case "canceled":

                        rtnerr = orderData.PurchaseInfo.GetXmlProperty("genxml/paymenterror");
                        rtnerr = "Mollie Canceled"; // to return this so a fail is activated.
                        return GetReturnTemplate(orderData, false, "Mollie canceled");

                    case "expired":

                        rtnerr = orderData.PurchaseInfo.GetXmlProperty("genxml/paymenterror");
                        rtnerr = "Mollie Expired"; // to return this so a fail is activated.
                        return GetReturnTemplate(orderData, false, "Mollie expired");

                    default:

                        rtnerr = orderData.PurchaseInfo.GetXmlProperty("genxml/paymenterror");
                        rtnerr = "Mollie failed"; // to return this so a fail is activated.
                        return GetReturnTemplate(orderData, false, "Mollie failed");

                }
            }
            else
            {
                rtnerr = orderData.PurchaseInfo.GetXmlProperty("genxml/paymenterror");
                rtnerr = "Mollie failed"; // to return this so a fail is activated.
                return GetReturnTemplate(orderData, false, "Mollie failed");
            }
        }
        #endregion

        #region GetReturnTemplate
        private string GetReturnTemplate(OrderData orderData, bool paymentok, string paymenterror)
        {
            var info = ProviderUtils.GetProviderSettings();
            info.UserId = UserController.Instance.GetCurrentUserInfo().UserID;
            var passSettings = NBrightBuyUtils.GetPassSettings(info);
            if (passSettings.ContainsKey("paymenterror"))
            {
                passSettings.Add("paymenterror", paymenterror);
            }
            var displaytemplate = "payment_ok.cshtml";

            if (paymentok)
            {
                info.SetXmlProperty("genxml/ordernumber", orderData.OrderNumber);
                info.SetXmlProperty("genxml/orderid", orderData.PurchaseInfo.ItemID.ToString());
                return NBrightBuyUtils.RazorTemplRender(displaytemplate, 0, "", info, "/DesktopModules/NBright/OS_Mollie", "config", Utils.GetCurrentCulture(), passSettings);
            }
            else
            {
                displaytemplate = "payment_fail.cshtml";
                return NBrightBuyUtils.RazorTemplRender(displaytemplate, 0, "", info, "/DesktopModules/NBright/OS_Mollie", "config", Utils.GetCurrentCulture(), passSettings);
            }
        }
        #endregion
    }
}