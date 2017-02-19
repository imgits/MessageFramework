#pragma once
#define SECURITY_WIN32
#include <windows.h>
#include <security.h>
#include <schannel.h>
#include <iostream>
#include <string>

class _SslStream
{
private:
	HCERTSTORE	   m_CertStore;
	PCCERT_CONTEXT m_pCertContext;
public:
	_SslStream();
	~_SslStream();
	PCCERT_CONTEXT FindCertificate(const std::string& name);

protected:
	bool InitCredential()
	{
		FreeCertContext(m_pCertContext);
		m_pCertContext = NULL;
		return true;
	}

	bool InitCredential(WCHAR* CertName)
	{
		FreeCertContext(m_pCertContext);
		m_pCertContext = FindCertificate(CertName);
		return  true;
	}

	bool InitCredential(WCHAR* CertFile, WCHAR* password)
	{
		FreeCertContext(m_pCertContext);
		m_pCertContext = FindCertificate(CertFile, password);
		return  true;
	}

	PCredHandle CreateCredentialsFromCertificate(PCCERT_CONTEXT pCertContext, bool IsClientCert)
	{
		// Build Schannel credential structure.
		SCHANNEL_CRED   SchannelCred = { 0 };
		SchannelCred.dwVersion = SCHANNEL_CRED_VERSION;
		if (pCertContext!=NULL)
		{
			SchannelCred.cCreds = 1;
			SchannelCred.paCred = &pCertContext;
		}
		SchannelCred.grbitEnabledProtocols = SP_PROT_TLS1_2_SERVER;
		SchannelCred.dwFlags = SCH_USE_STRONG_CRYPTO;

		SECURITY_STATUS Status;
		TimeStamp       tsExpiry;
		PCredHandle		pCredHandle;
		// Get a handle to the SSPI credential
		Status = ::AcquireCredentialsHandle(
			NULL,                   // Name of principal
			UNISP_NAME,           // Name of package
			IsClientCert ? SECPKG_CRED_OUTBOUND : SECPKG_CRED_INBOUND,    // Flags indicating use
			NULL,                   // Pointer to logon ID
			&SchannelCred,          // Package specific data
			NULL,                   // Pointer to GetKey() func
			NULL,                   // Value to pass to GetKey()
			pCredHandle,                // (out) Cred Handle
			&tsExpiry);             // (out) Lifetime (optional)

		if (Status != SEC_E_OK || !SecIsValidHandle(&pCredHandle))
		{
			DWORD dw = GetLastError();
			return NULL;
		}

		return pCredHandle;
	}

private:


	PCCERT_CONTEXT FindCertificate(WCHAR* certFilename, WCHAR* password)
	{
		HCERTSTORE hCertStore = LoadCertificateFromFile(certFilename, password);
		if (hCertStore == NULL) return NULL;
		PCCERT_CONTEXT pCertContext = CertEnumCertificatesInStore(hCertStore, NULL);
		return pCertContext;
	}

	PCCERT_CONTEXT FindCertificate(WCHAR* SubjectName)
	{
		HCERTSTORE hCertStore = OpenCertStore(false);
		if (hCertStore == NULL) return NULL;
		
		PCCERT_CONTEXT pCertContext=NULL;

		pCertContext = CertFindCertificateInStore(
			hCertStore,
			X509_ASN_ENCODING,
			0,
			CERT_FIND_SUBJECT_STR,
			SubjectName,
			NULL);

		return pCertContext;
	}

	HCERTSTORE OpenCertStore(bool UserStore = false)
	{
		HCERTSTORE hCertStore = NULL;
		if (UserStore) hCertStore = CertOpenSystemStoreW(NULL, L"my");
		else
		{
			hCertStore = CertOpenStore(CERT_STORE_PROV_SYSTEM,
				X509_ASN_ENCODING,
				NULL,
				CERT_STORE_OPEN_EXISTING_FLAG | CERT_STORE_READONLY_FLAG | CERT_SYSTEM_STORE_LOCAL_MACHINE,
				L"my");
		}

		if (hCertStore == NULL)
		{
			//HRESULT status = GetLastError();
		}
		return hCertStore;
	}

	HCERTSTORE LoadCertificateFromFile(WCHAR* certFilename, WCHAR* password)
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

		HCERTSTORE hCertStore = PFXImportCertStore(&blobData, password, 0);
		if (hCertStore == NULL) goto Error;

		CloseHandle(certFileHandle);
		delete[] certEncoded;
		return hCertStore;

	Error:
		HRESULT status = GetLastError();
		CloseHandle(certFileHandle);
		if (certEncoded != NULL) delete[] certEncoded;
		return NULL;
	}

	void CloseCertStore(HCERTSTORE hCertStore)
	{
		if (hCertStore != NULL)
		{
			::CertCloseStore(hCertStore, CERT_CLOSE_STORE_FORCE_FLAG);
		}
	}

	void FreeCertHandle(PSecHandle SecHandle)
	{
		if (SecIsValidHandle(SecHandle)) ::FreeCredentialsHandle(SecHandle);
	}

	void FreeCertContext(PCCERT_CONTEXT pCertContext)
	{
		if (pCertContext != NULL) ::CertFreeCertificateContext(pCertContext);
	}

};

