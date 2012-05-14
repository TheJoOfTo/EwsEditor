﻿using System;
using System.Net;
using EWSEditor.Common;
using EWSEditor.EwsVsProxy;
using EWSEditor.Logging;
using EWSEditor.Settings;
using EWSEditor.Common.Extensions;
using Microsoft.Exchange.WebServices.Data;

namespace EWSEditor.Exchange
{
    public class EwsProxyFactory
    {
        public static ExchangeVersion? RequestedExchangeVersion = null;
        public static bool? AllowAutodiscoverRedirect = null;
        public static bool? EnableScpLookup;
        public static NetworkCredential ServiceCredential = null;
        public static Microsoft.Exchange.WebServices.Data.EmailAddress ServiceEmailAddress = null;
        public static Uri EwsUrl;
        public static int? Timeout = null;
        public static bool? UseDefaultCredentials = null;
        public static ImpersonatedUserId UserToImpersonate = null;

        public static void DoAutodiscover()
        {
            DoAutodiscover(ServiceEmailAddress);
        }

        public static void DoAutodiscover(Microsoft.Exchange.WebServices.Data.EmailAddress emailAddress)
        {
            ExchangeService service = CreateExchangeService();
            service.AutodiscoverUrl(emailAddress.Address, ValidationCallbackHelper.RedirectionUrlValidationCallback );
            EwsUrl = service.Url;
        }

        public static ExchangeService CreateExchangeService()
        {
            ExchangeService service = null;

            if (RequestedExchangeVersion.HasValue)
            {
                service = new ExchangeService(RequestedExchangeVersion.Value);
            }
            else
            {
                service = new ExchangeService();
            }

            service.UserAgent = GlobalSettings.UserAgent;

            service.TraceEnabled = true;
            service.TraceListener = new EWSEditor.Logging.EwsTraceListener();

            if (EnableScpLookup.HasValue)
            {
                service.EnableScpLookup = EnableScpLookup.Value;
            }

            if (ServiceCredential != null)
            {
                service.Credentials = ServiceCredential;
            }

            if (EwsUrl != null)
            {
                service.Url = EwsUrl;
            }

            if (Timeout.HasValue)
            {
                service.Timeout = Timeout.Value;
            }

            if (UseDefaultCredentials.HasValue)
            {
                service.UseDefaultCredentials = UseDefaultCredentials.Value;
            }

            if (UserToImpersonate != null)
            {
                service.ImpersonatedUserId = UserToImpersonate;
            }

            return service;
        }

        public static ExchangeServiceBinding CreateExchangeServiceBinding()
        {
            var binding = new ExchangeServiceBinding();
            binding.AllowAutoRedirect = false;

            binding.UserAgent = GlobalSettings.UserAgent;

            // Set the RequestServerVersionValue
            binding.RequestServerVersionValue = new EwsVsProxy.RequestServerVersion();
            switch (RequestedExchangeVersion)
            {
                case ExchangeVersion.Exchange2007_SP1:
                    binding.RequestServerVersionValue.Version = ExchangeVersionType.Exchange2007_SP1;
                    break;
                case ExchangeVersion.Exchange2010:
                    binding.RequestServerVersionValue.Version = ExchangeVersionType.Exchange2010;
                    break;
                case ExchangeVersion.Exchange2010_SP1:
                    binding.RequestServerVersionValue.Version = ExchangeVersionType.Exchange2010_SP1;
                    break;
                default:
                    DebugLog.WriteVerbose("Requested ExchangeVersion was '" + RequestedExchangeVersion.Value.ToString() + "' which is not expected");
                    throw new ApplicationException("Unexpected ExchangeVersion");
            }

            if (EwsUrl != null)
            {
                binding.Url = EwsUrl.AbsoluteUri;
            }

            if (ServiceCredential != null)
            {
                binding.Credentials = ServiceCredential;
            }

            if (UseDefaultCredentials.HasValue)
            {
                binding.UseDefaultCredentials = UseDefaultCredentials.Value;
            }

            if (Timeout.HasValue)
            {
                binding.Timeout = Timeout.Value;
            }

            // Create the ExchangeImpersonationType if needed
            if (UserToImpersonate != null)
            {
                binding.ExchangeImpersonation = new ExchangeImpersonationType();
                binding.ExchangeImpersonation.ConnectingSID = new ConnectingSIDType();
                binding.ExchangeImpersonation.ConnectingSID.Item = UserToImpersonate.Id;
                switch (UserToImpersonate.IdType)
                {
                    case ConnectingIdType.PrincipalName:
                        binding.ExchangeImpersonation.ConnectingSID.ItemElementName = ItemChoiceType.PrincipalName;
                        break;
                    case ConnectingIdType.SID:
                        binding.ExchangeImpersonation.ConnectingSID.ItemElementName = ItemChoiceType.SID;
                        break;
                    case ConnectingIdType.SmtpAddress:
                        binding.ExchangeImpersonation.ConnectingSID.ItemElementName = ItemChoiceType.SmtpAddress;
                        break;
                }
            }

            return binding;
        }

        public static void InitializeWithDefaults()
        {
            InitializeWithDefaults(null, null, null);
        }

        /// <summary>
        /// Create a service binding based off of default credentials,
        /// the assumed root folder, and an assumed autodiscover email address
        /// </summary>
        /// <param name="version">EWS schema version to use.  Passing NULL uses the
        /// EWS Managed API default value.</param>
        /// <param name="ewsUrl">URL to EWS endpoint.  Passing NULL or an empty string
        /// results in a call to Autodiscover</param>
        /// <param name="autodiscoverAddress">Email address to use for Autodiscover.
        /// Passing NULL or an empty string results in a ActiveDirectory querying.</param>
        /// <returns>A new instance of an ExchangeService</returns>
        private static void InitializeWithDefaults(ExchangeVersion? version, Uri ewsUrl, string autodiscoverAddress)
        {
            RequestedExchangeVersion = version;
            UseDefaultCredentials = true;

            // If the EWS URL is not specified, use Autodiscover to find it
            if (ewsUrl == null)
            {
                // If no email address was given to use with Autodiscover, attempt
                // to look it up in Active Directory
                if (String.IsNullOrEmpty(autodiscoverAddress))
                {
                    autodiscoverAddress = ActiveDirectoryHelper.GetPrimarySmtp(
                        System.Security.Principal.WindowsIdentity.GetCurrent().Name);
                }

                DoAutodiscover(autodiscoverAddress);
            }
            else
            {
                EwsUrl = ewsUrl;
            }

            try
            {
                CreateExchangeService().TestExchangeService();
            }
            catch (ServiceVersionException ex)
            {
                DebugLog.WriteVerbose("Initial requested version of Exchange2010 didn't work, trying Exchange 2007_SP1", ex);
                // Pass the autodiscover email address and URL if we've already looked those up
                InitializeWithDefaults(ExchangeVersion.Exchange2007_SP1, EwsUrl, autodiscoverAddress);
            }

        }
    }
}
