#pragma once
#define SECURITY_WIN32
#include <windows.h>
#include <security.h>
#include <schannel.h>
#include <iostream>
#include <string>

#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "secur32.lib")
#pragma comment(lib, "ws2_32.lib")

typedef bool(__stdcall *SslOutputCallback)(BYTE* buffer, int count);

class _StreamSSL
{
private:
	CtxtHandle     m_CtxtHandle;
	CredHandle	   m_CredHandle;
	SecPkgContext_StreamSizes m_StreamSizes;
	WCHAR          m_ServerName[64];
	HANDLE		   m_AuthenticateEvent;
	SOCKET		   m_socket;
public:
	bool			  IsAuthenticated;
	SslOutputCallback TokenOutput;
	SslOutputCallback EncryptOutput;
	SslOutputCallback DecryptOutput;
public:
	_StreamSSL()
	{
		m_socket = NULL;
		IsAuthenticated = false;
		m_AuthenticateEvent = NULL;
		SecInvalidateHandle(&m_CtxtHandle);
		SecInvalidateHandle(&m_CredHandle);
		memset(&m_StreamSizes, 0, sizeof(m_StreamSizes));
		wcscpy(m_ServerName, L"localhost");
	}

	~_StreamSSL()
	{
		Close();
	}

public:
	bool CreateCredentials(PCCERT_CONTEXT pCertContext, DWORD Protocol, bool AsClient)
	{
		Close();
		printf("StreamSSL::CreateCredentials\n");
		m_AuthenticateEvent = CreateEvent(NULL, true, false, NULL);
		// Build Schannel credential structure.
		SCHANNEL_CRED   SchannelCred = { 0 };
		SchannelCred.dwVersion = SCHANNEL_CRED_VERSION;
		if (pCertContext != NULL)
		{
			SchannelCred.cCreds = 1;
			SchannelCred.paCred = &pCertContext;
		}

		SchannelCred.grbitEnabledProtocols = Protocol;// AsClient ?
			//SP_PROT_CLIENTS | SP_PROT_TLS1_1_CLIENT | SP_PROT_TLS1_2_CLIENT : 
			//SP_PROT_SERVERS | SP_PROT_TLS1_1_SERVER | SP_PROT_TLS1_2_SERVER;
		SchannelCred.dwFlags = SCH_USE_STRONG_CRYPTO;

		SECURITY_STATUS Status;
		TimeStamp       tsExpiry;
		// Get a handle to the SSPI credential
		Status = ::AcquireCredentialsHandle(
			NULL,                   // Name of principal
			UNISP_NAME,           // Name of package
			AsClient ? SECPKG_CRED_OUTBOUND :SECPKG_CRED_INBOUND,    // Flags indicating use
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

	bool InitAsServer(PCCERT_CONTEXT pCertContext = NULL)
	{
		printf("StreamSSL::InitAsServer\n");
		DWORD Protocol = SP_PROT_SERVERS | SP_PROT_TLS1_1_SERVER | SP_PROT_TLS1_2_SERVER;
		return CreateCredentials(pCertContext, Protocol, false);
	}

	bool InitAsClient(WCHAR* ServerName, DWORD Protocol, PCCERT_CONTEXT pCertContext = NULL)
	{
		printf("StreamSSL::InitAsClient\n");
		if (ServerName == NULL || wcslen(ServerName) >= sizeof(m_ServerName) / 2)
		{
			return false;
		}
		wcscpy(m_ServerName,ServerName);
		if (Protocol == 0) Protocol = SP_PROT_CLIENTS | SP_PROT_TLS1_1_CLIENT | SP_PROT_TLS1_2_CLIENT;
		if (!CreateCredentials(pCertContext, Protocol, true)) return false;
		return AuthenticateAsClient(NULL, 0);
	}

	bool AuthenticateAsClient(SOCKET socket, DWORD Protocol)
	{
		if (!CreateCredentials(NULL, Protocol, true)) return false;

		printf("StreamSSL::AuthenticateAsClient\n");

		DWORD dwSSPIFlags =
			ASC_REQ_SEQUENCE_DETECT |
			ASC_REQ_REPLAY_DETECT |
			ASC_REQ_CONFIDENTIALITY |
			ASC_REQ_EXTENDED_ERROR |
			ASC_REQ_ALLOCATE_MEMORY |
			ASC_REQ_STREAM |
			ISC_REQ_MANUAL_CRED_VALIDATION; //手工进行证书认证

		DWORD           dwSSPIOutFlags = 0;
		TimeStamp       tsExpiry;
		int				TokenSize = 0;
		char			TokenData[32 * 1024];

		SecBuffer InputBuffer[2] =
		{
			{ 0,	SECBUFFER_TOKEN,NULL },
			{ 0,	SECBUFFER_EMPTY,NULL }
		};
		SecBufferDesc InputBufferDesc = { SECBUFFER_VERSION, 2,InputBuffer };

		SecBuffer OutputBuffer = { 0,SECBUFFER_TOKEN,NULL };
		SecBufferDesc OutputBufferDesc = { SECBUFFER_VERSION, 1,&OutputBuffer };
		SECURITY_STATUS status = SEC_I_CONTINUE_NEEDED;

		do
		{
			if (InputBuffer->cbBuffer!=NULL)
			{
				TokenSize = ::recv(socket, TokenData, sizeof(TokenData), 0);
				if (TokenSize <= 0) return false;
				InputBuffer[0] = { (unsigned long)TokenSize,SECBUFFER_TOKEN,TokenData };
			}
			status = ::InitializeSecurityContext(
				&m_CredHandle,								// Which certificate to use, already established
				SecIsValidHandle(&m_CtxtHandle) ? &m_CtxtHandle : NULL,	// The context handle if we have one, ask to make one if this is first call
				m_ServerName,										// Input buffer list
				dwSSPIFlags,								// What we require of the connection
				0,
				SECURITY_NATIVE_DREP,													// Data representation, not used 
				&InputBufferDesc,
				0,
				&m_CtxtHandle,	// If we don't yet have a context handle, it is returned here
				&OutputBufferDesc,										// [out] The output buffer, for messages to be sent to the other end
				&dwSSPIOutFlags,								// [out] The flags associated with the negotiated connection
				&tsExpiry);

			switch (status)
			{
			case SEC_E_OK:
				IsAuthenticated = true;
				SetEvent(m_AuthenticateEvent);
				::QueryContextAttributes(
					&m_CtxtHandle,
					SECPKG_ATTR_STREAM_SIZES,
					&m_StreamSizes);
				printf("StreamSSL::Client.IsAuthenticated = true\n");
			case SEC_I_CONTINUE_NEEDED:
				if (OutputBuffer.cbBuffer > 0 && OutputBuffer.pvBuffer != NULL)
				{
					int bytes = ::send(socket, (char*)OutputBuffer.pvBuffer, OutputBuffer.cbBuffer, 0);
					::FreeContextBuffer(OutputBuffer.pvBuffer);
					if (bytes != OutputBuffer.cbBuffer) return false;
				}
			case SEC_E_INCOMPLETE_MESSAGE:
				break;
			default:
				return false;;
			}
		}while (status != SEC_E_OK);
		printf("StreamSSL::AuthenticateAsClient OK \n");
		return true;
	}

	bool AuthenticateAsServer(SOCKET socket, PCCERT_CONTEXT pCertContext)
	{
		DWORD Protocol = SP_PROT_SERVERS | SP_PROT_TLS1_1_SERVER | SP_PROT_TLS1_2_SERVER;
		if (!CreateCredentials(pCertContext, Protocol, false)) return false;
		
		printf("StreamSSL::AuthenticateAsServer\n");

		DWORD dwSSPIFlags =
			ASC_REQ_SEQUENCE_DETECT |
			ASC_REQ_REPLAY_DETECT |
			ASC_REQ_CONFIDENTIALITY |
			ASC_REQ_EXTENDED_ERROR |
			ASC_REQ_ALLOCATE_MEMORY |
			ASC_REQ_STREAM;
		DWORD                dwSSPIOutFlags = 0;
		TimeStamp            tsExpiry;
		int				     TokenSize = 0;
		char				 TokenData[32 * 1024];
		SECURITY_STATUS status = SEC_I_CONTINUE_NEEDED;
		SecBuffer InputBuffer[2] =
		{
			{ TokenSize,SECBUFFER_TOKEN,TokenData },
			{ 0,		SECBUFFER_EMPTY,NULL }
		};
		SecBufferDesc InputBufferDesc = { SECBUFFER_VERSION, 2,InputBuffer };

		SecBuffer OutputBuffer = { 0,SECBUFFER_TOKEN,NULL };
		SecBufferDesc OutputBufferDesc = { SECBUFFER_VERSION, 1,&OutputBuffer };
		do
		{
			int TokenSize = ::recv(socket, TokenData, sizeof(TokenData), 0);
			if (TokenSize <= 0) return false;
			InputBuffer[0] = { (unsigned long)TokenSize, SECBUFFER_TOKEN, TokenData };
			status = ::AcceptSecurityContext(
				&m_CredHandle,								// Which certificate to use, already established
				SecIsValidHandle(&m_CtxtHandle) ? &m_CtxtHandle : NULL,	// The context handle if we have one, ask to make one if this is first call
				&InputBufferDesc,										// Input buffer list
				dwSSPIFlags,									// What we require of the connection
				SECURITY_NATIVE_DREP,													// Data representation, not used 
				&m_CtxtHandle,	// If we don't yet have a context handle, it is returned here
				&OutputBufferDesc,										// [out] The output buffer, for messages to be sent to the other end
				&dwSSPIOutFlags,								// [out] The flags associated with the negotiated connection
				&tsExpiry);
			switch (status)
			{
			case SEC_E_OK:
				IsAuthenticated = true;
				SetEvent(m_AuthenticateEvent);
				::QueryContextAttributes(
					&m_CtxtHandle,
					SECPKG_ATTR_STREAM_SIZES,
					&m_StreamSizes);
				printf("StreamSSL::Server.IsAuthenticated = true\n");
			case SEC_I_CONTINUE_NEEDED:
				if (OutputBuffer.cbBuffer > 0 && OutputBuffer.pvBuffer != NULL)
				{
					int bytes = ::send(socket,(char*)OutputBuffer.pvBuffer, OutputBuffer.cbBuffer,0);
					::FreeContextBuffer(OutputBuffer.pvBuffer);
					if (bytes <= 0) return false;
				}
			case SEC_E_INCOMPLETE_MESSAGE:
				break;
			default:
				return false;
			}
		} while (status != SEC_E_OK);
		printf("StreamSSL::AuthenticateAsServer OK\n");
		return true;
	}


	bool AuthenticateAsClient(BYTE* TokenData, int TokenSize)
	{
		printf("StreamSSL::AuthenticateAsClient\n");
		if (TokenOutput == NULL) return false;
		if (!SecIsValidHandle(&m_CredHandle)) return false;

		DWORD dwSSPIFlags =
			ASC_REQ_SEQUENCE_DETECT |
			ASC_REQ_REPLAY_DETECT |
			ASC_REQ_CONFIDENTIALITY |
			ASC_REQ_EXTENDED_ERROR |
			ASC_REQ_ALLOCATE_MEMORY |
			ASC_REQ_STREAM |
			ISC_REQ_MANUAL_CRED_VALIDATION; //手工进行证书认证

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

		SECURITY_STATUS status = ::InitializeSecurityContext(
			&m_CredHandle,								// Which certificate to use, already established
			SecIsValidHandle(&m_CtxtHandle) ? &m_CtxtHandle : NULL,	// The context handle if we have one, ask to make one if this is first call
			m_ServerName,										// Input buffer list
			dwSSPIFlags,								// What we require of the connection
			0,
			SECURITY_NATIVE_DREP,													// Data representation, not used 
			&InputBufferDesc,
			0,
			&m_CtxtHandle,	// If we don't yet have a context handle, it is returned here
			&OutputBufferDesc,										// [out] The output buffer, for messages to be sent to the other end
			&dwSSPIOutFlags,								// [out] The flags associated with the negotiated connection
			&tsExpiry);

		switch (status)
		{
		case SEC_E_OK:
			IsAuthenticated = true;
			SetEvent(m_AuthenticateEvent);
			::QueryContextAttributes(
				&m_CtxtHandle,
				SECPKG_ATTR_STREAM_SIZES,
				&m_StreamSizes);
			printf("StreamSSL::Client.IsAuthenticated = true\n");
		case SEC_I_CONTINUE_NEEDED:
			if (OutputBuffer.cbBuffer >0 && OutputBuffer.pvBuffer != NULL)
			{
				TokenOutput((BYTE*)OutputBuffer.pvBuffer, OutputBuffer.cbBuffer);
				::FreeContextBuffer(OutputBuffer.pvBuffer);
			}
		case SEC_E_INCOMPLETE_MESSAGE:
			return true;
		default:
			break;
		}
		SetEvent(&m_AuthenticateEvent);
		return false;
	}

	bool AuthenticateAsServer(BYTE* TokenData, int TokenSize)
	{
		printf("StreamSSL::AuthenticateAsServer\n");
		if (TokenOutput == NULL) return false;
		if (!SecIsValidHandle(&m_CredHandle)) return false;

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
		switch (status)
		{
		case SEC_E_OK:
			IsAuthenticated = true;
			SetEvent(m_AuthenticateEvent);
			::QueryContextAttributes(
				&m_CtxtHandle,
				SECPKG_ATTR_STREAM_SIZES,
				&m_StreamSizes);
			printf("StreamSSL::Server.IsAuthenticated = true\n");
		case SEC_I_CONTINUE_NEEDED:
			if (OutputBuffer.cbBuffer >0 && OutputBuffer.pvBuffer != NULL)
			{
				TokenOutput((BYTE*)OutputBuffer.pvBuffer, OutputBuffer.cbBuffer);
				::FreeContextBuffer(OutputBuffer.pvBuffer);
			}
		case SEC_E_INCOMPLETE_MESSAGE:
			return true;
		default:
			break;
		}
		SetEvent(&m_AuthenticateEvent);
		return false;

	}

	//bool WaitForAuthenticate()
	//{
	//	DWORD result = ::WaitForSingleObject(m_AuthenticateEvent, INFINITE);
	//	if (result != WAIT_OBJECT_0)
	//	{
	//		DWORD err = GetLastError();
	//		return false;
	//	}
	//	return IsAuthenticated;
	//}

	bool EncryptData(BYTE* DataBuf, int DataSize)
	{
		if (EncryptOutput == NULL) return false;
		BYTE* EncryptBuffer = NULL;
		int   EncryptBufferSize = 0;
		int   EncryptedSize = 0;
		bool  result = false;
		while (DataSize >0)
		{
			int HeaderSize = m_StreamSizes.cbHeader;
			int MsgSize = DataSize <= m_StreamSizes.cbMaximumMessage ? DataSize : m_StreamSizes.cbMaximumMessage;
			int TrailerSize = m_StreamSizes.cbTrailer;
			int PacketSize = HeaderSize + MsgSize + TrailerSize;
			//尽量避免重复分配释放内存
			if (EncryptBufferSize < PacketSize)
			{
				if (EncryptBuffer != NULL) delete[] EncryptBuffer;
				EncryptBuffer = new BYTE[PacketSize];
				EncryptBufferSize = PacketSize;
			}
			memcpy(EncryptBuffer + HeaderSize, DataBuf+ EncryptedSize, DataSize);

			SecBuffer MessageBuffer[4] =
			{
				{ HeaderSize,	SECBUFFER_STREAM_HEADER,	EncryptBuffer },
				{ MsgSize,		SECBUFFER_DATA ,			EncryptBuffer + HeaderSize },
				{ TrailerSize,	SECBUFFER_STREAM_TRAILER,	EncryptBuffer + HeaderSize + MsgSize },
				{ 0,			SECBUFFER_EMPTY ,			NULL }
			};

			SecBufferDesc MsgBufferDesc = { SECBUFFER_VERSION, 4, MessageBuffer };

			SECURITY_STATUS status = ::EncryptMessage(
				&m_CtxtHandle,
				0,
				&MsgBufferDesc,
				0);
			if (status != SEC_E_OK)
			{
				result = false;
				break;
			}
			PacketSize = HeaderSize + MsgSize + MessageBuffer[2].cbBuffer;
			result = EncryptOutput(EncryptBuffer, PacketSize);
			if (!result) break;
			DataSize -= MsgSize;
			EncryptedSize += MsgSize;
		}
		if (EncryptBuffer != NULL) delete[] EncryptBuffer;
		return result;
	}

	bool DecryptData(BYTE* DataBuf, int DataSize)
	{
		if (DecryptOutput == NULL) return false;

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
			return true; 
		}
		else if (status == SEC_E_OK)
		{
			bool result = true;
			for (int i = 1; i < 4; i++)
			{
				if (DecryptBuffers[i].BufferType == SECBUFFER_DATA && DecryptBuffers[i].cbBuffer >0 && DecryptBuffers[i].pvBuffer!=NULL)
				{
					DecryptOutput((BYTE*)DecryptBuffers[i].pvBuffer, DecryptBuffers[i].cbBuffer);
					::FreeContextBuffer(DecryptBuffers[i].pvBuffer);
				}
				else if(DecryptBuffers[i].BufferType == SECBUFFER_EXTRA && DecryptBuffers[i].cbBuffer >0 && DecryptBuffers[i].pvBuffer != NULL)
				{//还有剩余数据，继续解密
					if (!DecryptData((BYTE*)DecryptBuffers[i].pvBuffer, DecryptBuffers[i].cbBuffer))
					{
						result = false;
					};
					::FreeContextBuffer(DecryptBuffers[i].pvBuffer);
				}
			}
			return result;
		}
		for (int i = 1; i < 4; i++)
		{
			if (DecryptBuffers[i].pvBuffer != NULL)
			{
				::FreeContextBuffer(&DecryptBuffers[i].pvBuffer);
			}
		}
		return false;
	}

	// In theory a connection may switch in and out of SSL mode.
	// This stops SSL, but it has not been tested
	void Close()
	{
		IsAuthenticated = false;
		if (m_AuthenticateEvent!=NULL) CloseHandle(m_AuthenticateEvent);
		m_AuthenticateEvent = NULL;
		if (SecIsValidHandle(&m_CredHandle)) ::FreeCredentialsHandle(&m_CredHandle);
		if (SecIsValidHandle(&m_CtxtHandle)) ::FreeCredentialsHandle(&m_CtxtHandle);
		SecInvalidateHandle(&m_CtxtHandle);
		SecInvalidateHandle(&m_CredHandle);
	}

	void Shutdown()
	{
		if (EncryptOutput == NULL) return;

		DWORD           dwType;
		PBYTE           pbMessage;
		DWORD           cbMessage;

		SecBufferDesc   OutBufferDesc;
		SecBuffer       OutBuffers[1];
		DWORD           dwSSPIFlags;
		DWORD           dwSSPIOutFlags;
		TimeStamp       tsExpiry;
		DWORD           Status;

		// Notify schannel that we are about to close the connection.

		dwType = SCHANNEL_SHUTDOWN;

		OutBuffers[0].pvBuffer = &dwType;
		OutBuffers[0].BufferType = SECBUFFER_TOKEN;
		OutBuffers[0].cbBuffer = sizeof(dwType);

		OutBufferDesc.cBuffers = 1;
		OutBufferDesc.pBuffers = OutBuffers;
		OutBufferDesc.ulVersion = SECBUFFER_VERSION;

		Status = ::ApplyControlToken(&m_CtxtHandle, &OutBufferDesc);

		if (FAILED(Status)) return;
			// Build an SSL close notify message.

		dwSSPIFlags =
			ASC_REQ_SEQUENCE_DETECT |
			ASC_REQ_REPLAY_DETECT |
			ASC_REQ_CONFIDENTIALITY |
			ASC_REQ_EXTENDED_ERROR |
			ASC_REQ_ALLOCATE_MEMORY |
			ASC_REQ_STREAM;

		OutBuffers[0].pvBuffer = NULL;
		OutBuffers[0].BufferType = SECBUFFER_TOKEN;
		OutBuffers[0].cbBuffer = 0;

		OutBufferDesc.cBuffers = 1;
		OutBufferDesc.pBuffers = OutBuffers;
		OutBufferDesc.ulVersion = SECBUFFER_VERSION;

		Status = ::AcceptSecurityContext(
			&m_CredHandle,			// Which certificate to use, already established
			&m_CtxtHandle,				// The context handle if we have one, ask to make one if this is first call
			NULL,							// Input buffer list
			dwSSPIFlags,				// What we require of the connection
			0,								// Data representation, not used 
			NULL,							// Returned context handle, not used, because we already have one
			&OutBufferDesc,					// [out] The output buffer, for messages to be sent to the other end
			&dwSSPIOutFlags,			// [out] The flags associated with the negotiated connection
			&tsExpiry);					// [out] Receives context expiration time

		if (FAILED(Status)) return;

		pbMessage = (PBYTE)OutBuffers[0].pvBuffer;
		cbMessage = OutBuffers[0].cbBuffer;

		// Send the close notify message to the client.

		if (pbMessage != NULL && cbMessage != 0)
		{
			EncryptOutput(pbMessage, cbMessage);
			::FreeContextBuffer(&OutBuffers[0]);
		}
	}

};

