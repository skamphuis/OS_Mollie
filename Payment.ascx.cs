
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using NBrightCore.common;
using NBrightCore.render;
using NBrightDNN;

using Nevoweb.DNN.NBrightBuy.Base;
using Nevoweb.DNN.NBrightBuy.Components;
using OS_Mollie.DNN.NBrightStore;
using DataProvider = DotNetNuke.Data.DataProvider;

namespace OS_Mollie
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// The ViewNBrightGen class displays the content
    /// </summary>
    /// -----------------------------------------------------------------------------
    public partial class OS_MolliePayment : NBrightBuyAdminBase
    {
        #region Event Handlers
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (Page.IsPostBack == false)
            {
                PageLoad();
            }
        }
        private void PageLoad()
        {
            if (NBrightBuyUtils.CheckRights())
            {
                var info = ProviderUtils.GetProviderSettings();
                
                var strOut = NBrightBuyUtils.RazorTemplRender("settings.cshtml", 0, "", info, ControlPath, "config", Utils.GetCurrentCulture(), StoreSettings.Current.Settings());
                var l = new Literal();
                l.Text = strOut;
                Controls.Add(l);
            }
        }
        #endregion
    }
}