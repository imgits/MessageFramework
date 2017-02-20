// SslServerStream.h
#pragma once
#include "_SslServerStream.h"
using namespace System;
using namespace System::IO;
using namespace System::Runtime::InteropServices;
using namespace System::Security::Cryptography::X509Certificates;

delegate bool  SslOutputDelegate(BYTE* buffer, int count);

public delegate bool  SslOutputHandler(array<Byte>^buffer, int offset,int count);

namespace StreamSSL
{
	public ref class SslServerStream
	{
	private:
		_SslServerStream  *m_SslServerStream;
		
		SslOutputDelegate^   m_SslServerTokenOutputDelegate;
		SslOutputDelegate^   m_SslEncryptedDataOutputDelegate;
		SslOutputDelegate^   m_SslDecryptedDataOutputDelegate;

	public:
		SslOutputHandler^ ServerTokenOutput;
		SslOutputHandler^ EncryptedDataOutput;
		SslOutputHandler^ DecryptedDataOutput;

		property bool IsAuthenticated
		{
			bool get() { return m_SslServerStream->IsAuthenticated;}
		}
		
	public:
		SslServerStream()
		{
			m_SslServerStream = new _SslServerStream();
			m_SslServerTokenOutputDelegate = gcnew SslOutputDelegate(this, &SslServerStream::SslServerTokenOutputCallback);
			IntPtr ptr = Marshal::GetFunctionPointerForDelegate(m_SslServerTokenOutputDelegate);
			SslOutputCallback cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_SslServerStream->ServerTokenOutput = cb;

			m_SslEncryptedDataOutputDelegate = gcnew SslOutputDelegate(this, &SslServerStream::SslEncryptedDataOutputCallback);
			ptr = Marshal::GetFunctionPointerForDelegate(m_SslEncryptedDataOutputDelegate);
			cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_SslServerStream->EncryptedDataOutput = cb;

			m_SslDecryptedDataOutputDelegate = gcnew SslOutputDelegate(this, &SslServerStream::SslDecryptedDataOutputCallback);
			ptr = Marshal::GetFunctionPointerForDelegate(m_SslDecryptedDataOutputDelegate);
			cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_SslServerStream->DecryptedDataOutput = cb;



		}
		
		~SslServerStream()
		{
			delete m_SslServerStream;
		}

		bool CreateCredentials(X509Certificate2^ x509Cert)
		{
			PCCERT_CONTEXT pCertContext = reinterpret_cast<PCCERT_CONTEXT>(x509Cert->Handle.ToPointer());
			return m_SslServerStream->CreateCredentials(pCertContext);
		}

		bool AcceptClientToken(array<Byte>^ buffer, int offset, int count)
		{
			if (buffer == nullptr || count <= 0) return false;
			BYTE* data = new BYTE[count];
			Marshal::Copy(buffer, offset, IntPtr(data), count); // This doesn't work
			return m_SslServerStream->AcceptClientToken(data, count);
		}

		bool Encrypt(array<Byte>^ buffer, int offset, int count)
		{
			if (buffer == nullptr || count <= 0) return false;
			BYTE* data = new BYTE[count];
			Marshal::Copy(buffer, offset, IntPtr(data), count); // This doesn't work
			return m_SslServerStream->EncryptData(data, count);
		}

		bool Decrypt(array<Byte>^ buffer, int offset, int count)
		{
			if (buffer == nullptr || count <= 0) return false;
			BYTE* data = new BYTE[count];
			Marshal::Copy(buffer, offset, IntPtr(data), count); // This doesn't work
			return m_SslServerStream->DecryptData(data, count);
		}
	private:
		bool SslServerTokenOutputCallback(BYTE* buffer, int count)
		{
			if (ServerTokenOutput == nullptr) return false;
			array<Byte>^ gcbuf = gcnew array<Byte>(count);
			Marshal::Copy((IntPtr)buffer, gcbuf, 0, count);
			return ServerTokenOutput(gcbuf, 0, count);
		}

		bool SslEncryptedDataOutputCallback(BYTE* buffer, int count)
		{
			if (EncryptedDataOutput == nullptr) return false;
			array<Byte>^ gcbuf = gcnew array<Byte>(count);
			Marshal::Copy((IntPtr)buffer, gcbuf, 0, count);
			return EncryptedDataOutput(gcbuf, 0, count);
		}

		bool SslDecryptedDataOutputCallback(BYTE* buffer, int count)
		{
			if (DecryptedDataOutput == nullptr) return false;
			array<Byte>^ gcbuf = gcnew array<Byte>(count);
			Marshal::Copy((IntPtr)buffer, gcbuf, 0, count);
			return DecryptedDataOutput(gcbuf, 0, count);
		}
	};
}
