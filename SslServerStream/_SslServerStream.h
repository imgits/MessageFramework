#pragma once
#define SECURITY_WIN32
#include <windows.h>
#include <security.h>
#include <schannel.h>
#include <iostream>
#include <string>

#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "secur32.lib")

typedef bool(__stdcall *SslOutputCallback)(BYTE* buffer, int count);

class _SslServerStream
{
private:
	CtxtHandle     m_CtxtHandle;
	CredHandle	   m_CredHandle;
public:
	bool		  IsAuthenticated;
	SslOutputCallback ServerTokenOutput;
	SslOutputCallback EncryptedDataOutput;
	SslOutputCallback DecryptedDataOutput;
public:
	_SslServerStream()
	{
		IsAuthenticated = false;
		SecInvalidateHandle(&m_CtxtHandle);
		SecInvalidateHandle(&m_CredHandle);
	}

	~_SslServerStream()
	{
		if (!SecIsValidHandle(&m_CredHandle)) ::FreeCredentialsHandle(&m_CredHandle);
		if (!SecIsValidHandle(&m_CtxtHandle)) ::FreeCredentialsHandle(&m_CtxtHandle);
		SecInvalidateHandle(&m_CtxtHandle);
		SecInvalidateHandle(&m_CredHandle);
	}

public:
	bool CreateCredentials(PCCERT_CONTEXT pCertContext)
	{
		// Build Schannel credential structure.
		SCHANNEL_CRED   SchannelCred = { 0 };
		SchannelCred.dwVersion = SCHANNEL_CRED_VERSION;
		if (pCertContext != NULL)
		{
			SchannelCred.cCreds = 1;
			SchannelCred.paCred = &pCertContext;
		}
		SchannelCred.grbitEnabledProtocols = SP_PROT_SERVERS;
		SchannelCred.dwFlags = SCH_USE_STRONG_CRYPTO;

		SECURITY_STATUS Status;
		TimeStamp       tsExpiry;
		PCredHandle		pCredHandle;
		// Get a handle to the SSPI credential
		Status = ::AcquireCredentialsHandle(
			NULL,                   // Name of principal
			UNISP_NAME,           // Name of package
			SECPKG_CRED_INBOUND,    // Flags indicating use
			NULL,                   // Pointer to logon ID
			&SchannelCred,          // Package specific data
			NULL,                   // Pointer to GetKey() func
			NULL,                   // Value to pass to GetKey()
			&m_CredHandle,            // (out) Cred Handle
			&tsExpiry);             // (out) Lifetime (optional)

		if (Status != SEC_E_OK || !SecIsValidHandle(&m_CredHandle))
		{
			DWORD dw = GetLastError();
			return false;
		}
		return true;

	}

	bool AcceptClientToken(BYTE* TokenData, int TokenSize)
	{
		DWORD dwSSPIFlags =
			ASC_REQ_SEQUENCE_DETECT |
			ASC_REQ_REPLAY_DETECT |
			ASC_REQ_CONFIDENTIALITY |
			ASC_REQ_EXTENDED_ERROR |
			ASC_REQ_ALLOCATE_MEMORY |
			ASC_REQ_STREAM;
		DWORD                dwSSPIOutFlags = 0;
		TimeStamp            tsExpiry;

		SecBuffer InputBuffer[2] =
		{
			{ TokenSize,SECBUFFER_TOKEN,TokenData },
			{ 0,		SECBUFFER_EMPTY,NULL }
		};
		SecBufferDesc InputBufferDesc = { SECBUFFER_VERSION, 2,InputBuffer };

		SecBuffer OutputBuffer = { 0,SECBUFFER_TOKEN,NULL };
		SecBufferDesc OutputBufferDesc = { SECBUFFER_VERSION, 1,&OutputBuffer };

		SECURITY_STATUS status = ::AcceptSecurityContext(
			&m_CredHandle,								// Which certificate to use, already established
			SecIsValidHandle(&m_CtxtHandle) ? &m_CtxtHandle : NULL,	// The context handle if we have one, ask to make one if this is first call
			&InputBufferDesc,										// Input buffer list
			dwSSPIFlags,									// What we require of the connection
			SECURITY_NATIVE_DREP,													// Data representation, not used 
			&m_CtxtHandle,	// If we don't yet have a context handle, it is returned here
			&OutputBufferDesc,										// [out] The output buffer, for messages to be sent to the other end
			&dwSSPIOutFlags,								// [out] The flags associated with the negotiated connection
			&tsExpiry);
		// Send requested output
		//if (status == SEC_E_OK) IsAuthenticated = true;
		
		if (status == SEC_E_OK || status == SEC_I_CONTINUE_NEEDED ||
			(FAILED(status) && (0 != (dwSSPIOutFlags & ASC_RET_EXTENDED_ERROR))))
		{
			if (OutputBuffer.cbBuffer >0 && OutputBuffer.pvBuffer != NULL)
			{
				if (ServerTokenOutput !=NULL) ServerTokenOutput((BYTE*)OutputBuffer.pvBuffer, OutputBuffer.cbBuffer);
				::FreeContextBuffer(&OutputBuffer);
			}
		}
		if (status == SEC_E_INCOMPLETE_MESSAGE || status == SEC_I_CONTINUE_NEEDED)
		{
			return true;
		}
		else if (status == SEC_E_OK)
		{
			IsAuthenticated = true;
			return true;
		}
		else
		{
			return false;
		}
		return false;

	}

	bool EncryptData(BYTE* DataBuf, int DataSize)
	{
		if (EncryptedDataOutput == NULL) return false;

		// encrypt the pre buffer to the write buffer
		SecPkgContext_Sizes sizes;
		::QueryContextAttributes(
			&m_CtxtHandle,
			SECPKG_ATTR_SIZES,
			&sizes);

		SecPkgContext_StreamSizes streamSizes;
		::QueryContextAttributes(
			&m_CtxtHandle,
			SECPKG_ATTR_STREAM_SIZES,
			&streamSizes);

		DWORD cbHeader = streamSizes.cbHeader;
		DWORD cbMsg = static_cast<DWORD>(DataSize);
		DWORD cbTrailer = streamSizes.cbTrailer;
		DWORD cbPacket = cbHeader + cbMsg + cbTrailer;

		BYTE* EncryptedMsg = new BYTE[cbPacket];

		memcpy(EncryptedMsg + cbHeader, DataBuf, DataSize);

		SecBuffer MessageBuffer[4] = 
		{ 
			{ cbHeader,	SECBUFFER_STREAM_HEADER,	EncryptedMsg },
			{ cbMsg,	SECBUFFER_DATA ,			EncryptedMsg + cbHeader },
			{ cbTrailer,SECBUFFER_STREAM_TRAILER,	EncryptedMsg + cbHeader + cbMsg },
			{ 0,		SECBUFFER_EMPTY ,			NULL }
		};

		SecBufferDesc MsgBufferDesc = { SECBUFFER_VERSION, 4, MessageBuffer};

		SECURITY_STATUS status = ::EncryptMessage(
			&m_CtxtHandle,
			0,
			&MsgBufferDesc,
			0);
		if (status != SEC_E_OK)
		{
			delete[] EncryptedMsg;
			return false;
		}
		bool result = EncryptedDataOutput(EncryptedMsg, cbPacket);
		delete[] EncryptedMsg;
		return result;
	}


	bool DecryptData(BYTE* DataBuf, int DataSize)
	{
		if (DecryptedDataOutput == NULL) return false;

		// Prepare decryption buffers
		SecBuffer DecryptBuffers[4]=
		{
			{ DataSize, SECBUFFER_DATA, DataBuf },
			{ 0, SECBUFFER_EMPTY, nullptr },
			{ 0, SECBUFFER_EMPTY, nullptr },
			{ 0, SECBUFFER_EMPTY, nullptr }
		};

		SecBufferDesc DecryptBufferDesc = { SECBUFFER_VERSION, 4, DecryptBuffers };

		// Decrypt
		SECURITY_STATUS status = ::DecryptMessage(
			&m_CtxtHandle, 
			&DecryptBufferDesc, 0, NULL);

		if (status == SEC_E_INCOMPLETE_MESSAGE)
		{
			return true; // Disconnect signal
		}
		else if (status == SEC_E_OK)
		{
			bool result = true;
			for (int i = 1; i < 4; i++)
			{
				if (DecryptBuffers[i].BufferType == SECBUFFER_DATA && DecryptBuffers[i].cbBuffer >0 && DecryptBuffers[i].pvBuffer!=NULL)
				{
					DecryptedDataOutput((BYTE*)DecryptBuffers[i].pvBuffer, DecryptBuffers[i].cbBuffer);
					if (DecryptBuffers[i].pvBuffer != DataBuf) ::FreeContextBuffer(&DecryptBuffers[i].pvBuffer);
				}
				else if(DecryptBuffers[i].BufferType == SECBUFFER_EXTRA && DecryptBuffers[i].cbBuffer >0 && DecryptBuffers[i].pvBuffer != NULL)
				{
					if (!DecryptData((BYTE*)DecryptBuffers[i].pvBuffer, DecryptBuffers[i].cbBuffer))
					{
						result = false;
					};
					if (DecryptBuffers[i].pvBuffer != DataBuf) ::FreeContextBuffer(&DecryptBuffers[i].pvBuffer);
				}
			}
			return result;
		}
		return false;
	}



};

