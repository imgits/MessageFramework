#pragma once
#define SECURITY_WIN32
#include <windows.h>
#include <security.h>
#include <schannel.h>
#include <iostream>
#include <string>

#pragma unmanaged
template<int nBuffs>
class CAutoSecBuffer : public SecBufferDesc
{
	SecBuffer   m_SecBuffer[nBuffs];
	bool        m_bRelease;

public:
	CAutoSecBuffer(bool bRelease)
		:m_bRelease(bRelease)
	{
		cBuffers = nBuffs;
		pBuffers = m_SecBuffer;
		ulVersion = SECBUFFER_VERSION;
	}
	~CAutoSecBuffer()
	{
		if (m_bRelease)
		{
			for (int i = 0; i<nBuffs; ++i)
			{
				m_SecBuffer[i].pvBuffer = NULL;
			}
		}

	}
	void FreeBuffer(int nBuff)
	{
		::FreeContextBuffer(m_SecBuffer[nBuff].pvBuffer);
		m_SecBuffer[nBuff].pvBuffer = NULL;
	}

	void SetSecBufferToken(int nBuff, void* pvBuffer, int nLen)
	{
		m_SecBuffer[nBuff].BufferType = SECBUFFER_TOKEN;
		m_SecBuffer[nBuff].cbBuffer = nLen;
		m_SecBuffer[nBuff].pvBuffer = pvBuffer;
	}
	void SetSecBufferData(int nBuff, void* pvBuffer, int nLen)
	{
		m_SecBuffer[nBuff].BufferType = SECBUFFER_DATA;
		m_SecBuffer[nBuff].cbBuffer = nLen;
		m_SecBuffer[nBuff].pvBuffer = pvBuffer;
	}
	void SetSecBufferStreamHeader(int nBuff, void* pvBuffer, int nLen)
	{
		m_SecBuffer[nBuff].BufferType = SECBUFFER_STREAM_HEADER;
		m_SecBuffer[nBuff].cbBuffer = nLen;
		m_SecBuffer[nBuff].pvBuffer = pvBuffer;
	}
	void SetSecBufferStreamTrailer(int nBuff, void* pvBuffer, int nLen)
	{
		m_SecBuffer[nBuff].BufferType = SECBUFFER_STREAM_TRAILER;
		m_SecBuffer[nBuff].cbBuffer = nLen;
		m_SecBuffer[nBuff].pvBuffer = pvBuffer;
	}
	void SetSecBufferEmpty(int nBuff)
	{
		m_SecBuffer[nBuff].BufferType = SECBUFFER_EMPTY;
		m_SecBuffer[nBuff].cbBuffer = 0;
		m_SecBuffer[nBuff].pvBuffer = NULL;
	}
	SecBuffer& operator[](unsigned int i)
	{
		return m_SecBuffer[i];
	}

};
