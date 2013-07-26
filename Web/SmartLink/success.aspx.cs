﻿using System;
using System.Collections;
using BankNet.Core;
using BankNet.Core.Provider;
using BankNet.Data;
using BankNet.Entity;
using Web.Ajax;
using Web.Helper;
using Web.WsPaymentTest;

namespace Web.SmartLink
{
    public partial class success : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

            string sTranId = Request.QueryString["vpc_MerchTxnRef"];
            if (string.IsNullOrEmpty(sTranId)) Response.Redirect("/SmartLink/", true);

            //Check info Smartlink
            Hashtable hash = new Hashtable();
            foreach (String key in Request.QueryString.AllKeys)
            {
                if (key.StartsWith("vpc_"))
                {
                    hash.Add(key, Request.QueryString[key]);
                }
            }

            bool isEmptysecureSecret;
            bool isValidsecureHash= SmartLinkHelper.checkSum(hash,out isEmptysecureSecret);
            if (isEmptysecureSecret) Response.Redirect("/SmartLink/", true);
            if(!isValidsecureHash)  Response.Redirect("/SmartLink/", true);

            //check info AV
            CacheInfo oCache = (CacheInfo)CacheProvider.Get(string.Format(KeyCache.KeyUserSmartlink, sTranId));
            if (oCache == null) Response.Redirect("/SmartLink/", true);

            SmartlinkQueryInfo oQueryInfo = new SmartlinkQueryInfo()
            {
                CreateDate = DateTime.Now
            };
            try
            {
                string sStatus = SmartLinkHelper.GetQuery(sTranId, ref oQueryInfo);
                if (!string.IsNullOrEmpty(sStatus))
                {
                    String[] arr = sStatus.Split('&');
                    foreach (String item in arr)
                    {
                        String[] temp = item.Split('=');
                        if ("vpc_DRExists".ToUpper().Equals(temp[0].ToUpper()))
                        {
                            oQueryInfo.vpc_DRExists = temp[1];
                        }
                        if ("vpc_FoundMultipleDRs".ToUpper().Equals(temp[0].ToUpper()))
                        {
                            oQueryInfo.vpc_FoundMultipleDRs = temp[1];
                        }
                        if ("vpc_Message".ToUpper().Equals(temp[0].ToUpper()))
                        {
                            oQueryInfo.vpc_Message = temp[1];
                        }
                        if ("vpc_SecureHash".ToUpper().Equals(temp[0].ToUpper()))
                        {
                            oQueryInfo.vpc_SecureHash = temp[1];
                        }
                        if ("vpc_TxnResponseCode".ToUpper().Equals(temp[0].ToUpper()))
                        {
                            oQueryInfo.vpc_TxnResponseCode = temp[1];
                        }
                    }

                    //giao dịch thành công
                    if(oQueryInfo.vpc_TxnResponseCode=="0")
                    {
                        //submit voucher
                        SubmitVoucherInfo oSVInfo = new SubmitVoucherInfo()
                        {
                            GatePayId = Config.ClientIdBanknet,
                            UserId = oCache.User.subnum,
                            Amount = int.Parse(oCache.Voucher.vouchervalue),
                            CreateDate = DateTime.Now,
                            TransId = sTranId
                        };
                        try
                        {
                            WSClient wsclient = new WSClient();
                            var cred = new credential { clientId = Config.ClientIdSmartLink };
                            var wsResult = wsclient.submitVoucher(cred, oSVInfo.UserId, oSVInfo.Amount.ToString(), oSVInfo.TransId);

                            oSVInfo.returnCode = wsResult.returnCode;
                            oSVInfo.returnCodeDescription = wsResult.returnCodeDescription;
                            oSVInfo.responseData = wsResult.responseData;
                            oSVInfo.signature = wsResult.signature;

                            if (oSVInfo.returnCode == "")
                            {
                                string sResultDate = XMLReader.ReadResultVocher(oSVInfo.responseData);//dt
                                Session[Config.GetSessionsResultDate] = sResultDate;//ss

                                Response.Redirect("/SmartLink/#" + sTranId + "|T", false);
                            }
                            else
                            {
                                //Session[Config.GetSessionsResultFail] = wsResult.returnCodeDescription;//ss
                                Session[Config.GetSessionsResultFail] = "Giao dịch không thành công";

                                Response.Redirect("/SmartLink/#" + sTranId + "|F|Y", false);
                            }
                        }
                        catch (Exception ex)
                        {
                            //log error
                            Session[Config.GetSessionsResultFail] = ex.Message;
                            oSVInfo.returnCode = ex.GetHashCode().ToString();
                            oSVInfo.returnCodeDescription = ex.Message;
                            Response.Redirect("/SmartLink/#" + sTranId + "|F|Y", false);
                        }
                        finally
                        {
                            SubmitVoucherData.instance.Add(oSVInfo);
                        }
                    }
                    else
                    {
                        //Session[Config.GetSessionsResultFail] = SmartLinkHelper.getResponseDescription(oQueryInfo.vpc_TxnResponseCode??"");
                        Session[Config.GetSessionsResultFail] = "Giao dịch không thành công";
                        Response.Redirect("/SmartLink/#" + sTranId + "|F", false);
                    }
                }
                else
                {
                    Response.Redirect("/SmartLink/#" + sTranId + "|F", false);
                }
            }
            catch (Exception ex)
            {
                oQueryInfo.vpc_TxnResponseCode = ex.GetHashCode().ToString();
                oQueryInfo.vpc_Message = ex.Message;
                Response.Redirect("/SmartLink/#" + sTranId + "|F", false);
                //throw;
            }
            finally
            {
                CacheProvider.Remove(string.Format(KeyCache.KeyUserSmartlink, sTranId));
                SmartlinkQueryData.instance.Add(oQueryInfo);
            }
        }
    }
}