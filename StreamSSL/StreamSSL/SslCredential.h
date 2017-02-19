//https://github.com/irinabov/debian-qpid-cpp-1.35.0/blob/2db67d01fe5e39eca22cfdac8b18872bea022f37/src/qpid/sys/windows/SslCredential.cpp
#pragma once
#define SECURITY_WIN32
#include <windows.h>
#include <security.h>
#include <schannel.h>
#include <iostream>
#include <string>
#include <vector>

class SslCredential
{
private:
	HCERTSTORE		m_hCertStore;
	PCCERT_CONTEXT	m_pCertContext;
	SCHANNEL_CRED	m_SchannelCred;
	CredHandle		m_CredHandle;
	TimeStamp		credExpiry;

public:

	SslCredential::SslCredential()
	{
		m_hCertStore = NULL;
		m_pCertContext = NULL;

		SecInvalidateHandle(&m_CredHandle);
		memset(&m_SchannelCred, 0, sizeof(m_SchannelCred));
		m_SchannelCred.dwVersion = SCHANNEL_CRED_VERSION;
		m_SchannelCred.dwFlags = SCH_CRED_NO_DEFAULT_CREDS;
	}

	SslCredential::~SslCredential()
	{
		if (SecIsValidHandle(&m_CredHandle)) ::FreeCredentialsHandle(&m_CredHandle);
		if (m_pCertContext) ::CertFreeCertificateContext(m_pCertContext);
		if (m_hCertStore ) ::CertCloseStore(m_hCertStore, CERT_CLOSE_STORE_FORCE_FLAG);
	}

	PCCERT_CONTEXT SslCredential::FindCertificate(WCHAR* SubjectName, WCHAR* certFilename=NULL, WCHAR* password=NULL)
	{
		if (certFilename == NULL)
		{
			if (!OpenCertStore(false)) return NULL;
		}
		else
		{
			if (!LoadCertificateFromFile(certFilename, password)) return NULL;
		}
		if (m_pCertContext) ::CertFreeCertificateContext(m_pCertContext);
		m_pCertContext = NULL;
		
		m_pCertContext = CertFindCertificateInStore(
			m_hCertStore,
			X509_ASN_ENCODING,
			0,
			CERT_FIND_SUBJECT_STR,
			SubjectName,
			NULL);

		WCHAR		pszFriendlyNameString[1024];
		while (m_pCertContext = CertEnumCertificatesInStore(m_hCertStore, m_pCertContext))
		{
			DWORD len = CertGetNameString(m_pCertContext, CERT_NAME_FRIENDLY_DISPLAY_TYPE, 0, NULL, pszFriendlyNameString, sizeof(pszFriendlyNameString)/sizeof(WCHAR));
			wprintf(L"%ws\n", pszFriendlyNameString);
			if (wcscmp(SubjectName, pszFriendlyNameString) == 0) break;
		}

		return m_pCertContext;
	}

	PCredHandle CreateClientCredential(PCCERT_CONTEXT pCertContext)
	{
		CreateCredentialsFromCertificate(pCertContext, true);
	}

	PCredHandle CreateServerCredential(bool IsClientCert, PCCERT_CONTEXT pCertContext)
	{
		CreateCredentialsFromCertificate(pCertContext, false);
	}

	PCredHandle CreateCredentialsFromCertificate(PCCERT_CONTEXT pCertContext, bool IsClientCert)
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
		PCredHandle		pCredHandle;
		// Get a handle to the SSPI credential
		Status = ::AcquireCredentialsHandle(
			NULL,                   // Name of principal
			UNISP_NAME,           // Name of package
			IsClientCert? SECPKG_CRED_OUTBOUND:SECPKG_CRED_INBOUND,    // Flags indicating use
			NULL,                   // Pointer to logon ID
			&SchannelCred,          // Package specific data
			NULL,                   // Pointer to GetKey() func
			NULL,                   // Value to pass to GetKey()
			pCredHandle,                // (out) Cred Handle
			&tsExpiry);             // (out) Lifetime (optional)

		if (Status != SEC_E_OK || !SecIsValidHandle(&m_CredHandle))
		{
			DWORD dw = GetLastError();
			return NULL;
		}

		return pCredHandle;
	}

private:
	bool SslCredential::LoadCertificateFromFile(WCHAR* certFilename, WCHAR* password)
	{
		HANDLE certFileHandle = CreateFile(certFilename, GENERIC_READ, 0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
		if (INVALID_HANDLE_VALUE == certFileHandle)
		{
			return false;
		}
		BYTE* certEncoded = NULL;
		DWORD certEncodedSize = 0L;
		const DWORD fileSize = GetFileSize(certFileHandle, NULL);
		if (INVALID_FILE_SIZE == fileSize)  goto Error;

		certEncoded = new BYTE[fileSize];
		bool result = ReadFile(certFileHandle, certEncoded, fileSize, &certEncodedSize, NULL);
		if (!result) goto Error;
		CloseHandle(certFileHandle);

		CRYPT_DATA_BLOB blobData;
		blobData.cbData = certEncodedSize;
		blobData.pbData = certEncoded;

		if (m_hCertStore) ::CertCloseStore(m_hCertStore, CERT_CLOSE_STORE_FORCE_FLAG);
		m_hCertStore = PFXImportCertStore(&blobData, password, 0);
		if (m_hCertStore == NULL) goto Error;

		CloseHandle(certFileHandle);
		delete[] certEncoded;
		return true;

	Error:
		HRESULT status = GetLastError();
		CloseHandle(certFileHandle);
		if (certEncoded != NULL) delete[] certEncoded;
		return false;
	}


	bool SslCredential::OpenCertStore(bool UserStore = false)
	{
		if (m_hCertStore) ::CertCloseStore(m_hCertStore, CERT_CLOSE_STORE_FORCE_FLAG);
		if (UserStore) m_hCertStore = CertOpenSystemStoreW(NULL, L"my");
		else
		{
			m_hCertStore = CertOpenStore(CERT_STORE_PROV_SYSTEM,
				X509_ASN_ENCODING,
				NULL,
				CERT_STORE_OPEN_EXISTING_FLAG | CERT_STORE_READONLY_FLAG | CERT_SYSTEM_STORE_LOCAL_MACHINE,
				L"my");
		}

		if (m_hCertStore != NULL) return true;
		HRESULT status = GetLastError();
		return false;
	}

};

