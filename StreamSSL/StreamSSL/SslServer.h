#pragma once
#define SECURITY_WIN32
#include <windows.h>
#include <security.h>
#include <schannel.h>
#include <iostream>
#include <string>

class SslServer
{
public:
	SslServer();
	~SslServer();
	int Encrypt()
	{
		CtxtHandle mContext;
		SecPkgContext_Sizes sizes;
		QueryContextAttributes(
			&mContext,
			SECPKG_ATTR_SIZES,
			&sizes);
	}

	PCCERT_CONTEXT CertFindServerByName(char*  SubjectName, bool UserStore=false)
	{
		HCERTSTORE  hMyCertStore = NULL;
		TCHAR		pszFriendlyNameString[128];
		TCHAR		pszNameString[128];

		if (UserStore)
		{
			hMyCertStore = CertOpenSystemStoreW(NULL, L"MY");
		}
		else
		{	// Open the local machine certificate store.
			hMyCertStore = CertOpenStore(CERT_STORE_PROV_SYSTEM,
				X509_ASN_ENCODING,
				NULL,
				CERT_STORE_OPEN_EXISTING_FLAG | CERT_STORE_READONLY_FLAG | CERT_SYSTEM_STORE_LOCAL_MACHINE,
				L"MY");
		}

		if (!hMyCertStore) 
		{
			int err = GetLastError();
			if (err == ERROR_ACCESS_DENIED);
				//DebugMsg("**** CertOpenStore failed with 'access denied'");
			else;
				//DebugMsg("**** Error %d returned by CertOpenStore", err);
			return NULL;
		}

		PCCERT_CONTEXT pCertContext = NULL;

		char * serverauth = szOID_PKIX_KP_SERVER_AUTH;
		CERT_ENHKEY_USAGE eku;
		PCCERT_CONTEXT pCertContextSaved = NULL;
		eku.cUsageIdentifier = 1;
		eku.rgpszUsageIdentifier = &serverauth;
		// Find a server certificate. Note that this code just searches for a 
		// certificate that has the required enhanced key usage for server authentication
		// it then selects the best one (ideally one that contains the server name
		// in the subject name).

		while (
			NULL != (pCertContext = CertFindCertificateInStore(hMyCertStore,
				X509_ASN_ENCODING,
				CERT_FIND_OPTIONAL_ENHKEY_USAGE_FLAG,
				CERT_FIND_ENHKEY_USAGE,
				&eku,
				pCertContext)))
		{
			//ShowCertInfo(pCertContext);
			if (!CertGetNameString(pCertContext, CERT_NAME_FRIENDLY_DISPLAY_TYPE, 0, NULL, pszFriendlyNameString, sizeof(pszFriendlyNameString)))
			{
				//DebugMsg("CertGetNameString failed getting friendly name.");
				continue;
			}
		}

		return NULL;
	}

	SECURITY_STATUS CreateCredentialsFromCertificate(PCredHandle phCreds, PCCERT_CONTEXT pCertContext)
	{
		// Build Schannel credential structure.
		SCHANNEL_CRED   SchannelCred = { 0 };
		SchannelCred.dwVersion = SCHANNEL_CRED_VERSION;
		SchannelCred.cCreds = 1;
		SchannelCred.paCred = &pCertContext;
		SchannelCred.grbitEnabledProtocols = SP_PROT_TLS1_2_SERVER;
		SchannelCred.dwFlags = SCH_USE_STRONG_CRYPTO;

		SECURITY_STATUS Status;
		TimeStamp       tsExpiry;
		// Get a handle to the SSPI credential
		Status = AcquireCredentialsHandle(
			NULL,                   // Name of principal
			UNISP_NAME,           // Name of package
			SECPKG_CRED_INBOUND,    // Flags indicating use
			NULL,                   // Pointer to logon ID
			&SchannelCred,          // Package specific data
			NULL,                   // Pointer to GetKey() func
			NULL,                   // Value to pass to GetKey()
			phCreds,                // (out) Cred Handle
			&tsExpiry);             // (out) Lifetime (optional)

		if (Status != SEC_E_OK)
		{
			DWORD dw = GetLastError();
			if (Status == SEC_E_UNKNOWN_CREDENTIALS);
				//DebugMsg("**** Error: 'Unknown Credentials' returned by AcquireCredentialsHandle. Be sure app has administrator rights. LastError=%d", dw);
			else;
				//DebugMsg("**** Error 0x%x returned by AcquireCredentialsHandle. LastError=%d.", Status, dw);
			return Status;
		}

		return SEC_E_OK;
	}
};

