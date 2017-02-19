#include "stdafx.h"
#include "_SslStream.h"


_SslStream::_SslStream()
{
}


_SslStream::~_SslStream()
{
}

PCCERT_CONTEXT CertFindServerByName(char*  SubjectName, bool UserStore = false)
{
	HCERTSTORE  hMyCertStore = NULL;
	TCHAR		pszFriendlyNameString[128];
	TCHAR		pszNameString[128];

	if (UserStore) hMyCertStore = CertOpenSystemStoreW(NULL, L"MY");
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


	return NULL;
}

PCCERT_CONTEXT _SslStream::FindCertificate(const std::string& name)
{
	// search for the certificate by Friendly Name
	PCCERT_CONTEXT tmpctx = NULL;
	return tmpctx;
}