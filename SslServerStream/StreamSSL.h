// StreamSSL.h
#pragma once
#include "_StreamSSL.h"
#include <vcclr.h>

using namespace System;
using namespace System::IO;
using namespace System::Text;
using namespace System::Runtime::InteropServices;
using namespace System::Security::Cryptography::X509Certificates;
using namespace System::Security::Authentication;

delegate bool  SslOutputDelegate(BYTE* buffer, int count);

public delegate bool  SslOutputHandler(array<Byte>^buffer, int offset,int count);

namespace SecStream
{
	public ref class StreamSSL
	{
	private:
		_StreamSSL  *m_StreamSSL;
		
		SslOutputDelegate^   m_SslTokenOutputDelegate;
		SslOutputDelegate^   m_SslEncryptDataOutputDelegate;
		SslOutputDelegate^   m_SslDecryptDataOutputDelegate;
		bool				 m_InitializeAsClient;

	public:
		//SslOutputHandler^ TokenOutput;
		SslOutputHandler^ EncryptDataOutput;
		SslOutputHandler^ DecryptDataOutput;

		//��֤�����:return true
		//��֤û����ɻ���ʧ��:return false
		property bool IsAuthenticated
		{
			bool get() { return m_StreamSSL->IsAuthenticated;}
		}
		
	public:
		StreamSSL()
		{
			//TokenOutput = nullptr;
			EncryptDataOutput = nullptr;
			DecryptDataOutput = nullptr;
			m_InitializeAsClient = false;

			//�������� _StreamSSL ����
			m_StreamSSL = new _StreamSSL();

			//���ûص�����_StreamSSL::TokenOutput
			m_SslTokenOutputDelegate = gcnew SslOutputDelegate(this, &StreamSSL::SslTokenOutputCallback);
			IntPtr ptr = Marshal::GetFunctionPointerForDelegate(m_SslTokenOutputDelegate);
			SslOutputCallback cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_StreamSSL->TokenOutput = cb;

			//���ûص�����_StreamSSL::EncryptDataOutput
			m_SslEncryptDataOutputDelegate = gcnew SslOutputDelegate(this, &StreamSSL::SslEncryptDataOutputCallback);
			ptr = Marshal::GetFunctionPointerForDelegate(m_SslEncryptDataOutputDelegate);
			cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_StreamSSL->EncryptDataOutput = cb;

			//���ûص�����_StreamSSL::DecryptDataOutput
			m_SslDecryptDataOutputDelegate = gcnew SslOutputDelegate(this, &StreamSSL::SslDecryptDataOutputCallback);
			ptr = Marshal::GetFunctionPointerForDelegate(m_SslDecryptDataOutputDelegate);
			cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_StreamSSL->DecryptDataOutput = cb;
		}
		
		~StreamSSL()
		{
			m_StreamSSL->Shutdown();
			m_StreamSSL->Close();
			delete m_StreamSSL;
			m_StreamSSL = NULL;
		}

		//��ʼ���ͻ��ˣ�Ĭ��ʹ��SSL3.0��TLS1.0Э�飬��֤��
		bool Initialize(String^ TargetHost)
		{
			return Initialize(TargetHost, SslProtocols::Default, nullptr);
		}

		//��ʼ���ͻ��ˣ�ʹ��ָ��Э�飬��֤��
		bool Initialize(String^ TargetHost, SslProtocols Protocol)
		{
			return Initialize(TargetHost, Protocol, nullptr);
		}

		//��ʼ���ͻ��ˣ�ʹ��ָ��Э���֤��
		bool Initialize(String^ TargetHost, SslProtocols Protocol, X509Certificate2^ x509Cert)
		{
			m_InitializeAsClient = true;
			if (TargetHost == nullptr)
			{
				throw gcnew Exception("StreamSSL::Initialize() 'TargetHost' is null");
			}
			pin_ptr<const wchar_t> ServerName = PtrToStringChars(TargetHost);
			DWORD _SslProtocol = 0;
			//int iProtocol = (int)(System::Int32^)Protocol;
			int iProtocol = (int)Protocol;
			switch (iProtocol)
			{
			case 0:	_SslProtocol = 0; break;//SslProtocols::None
			case 12:_SslProtocol = SP_PROT_SSL2_CLIENT; break; //SslProtocols::Ssl2
			case 48:_SslProtocol = SP_PROT_SSL3_CLIENT; break; //SslProtocols::Ssl3
			case 192:_SslProtocol = SP_PROT_TLS1_0_CLIENT; break; //SslProtocols::Tls
			case 768:_SslProtocol = SP_PROT_TLS1_1_CLIENT; break; //SslProtocols::Tls11
			case 3072:_SslProtocol = SP_PROT_TLS1_2_CLIENT; break; //SslProtocols::Tls12
			case 240://SslProtocols::Default
			default:
				_SslProtocol = SP_PROT_SSL3_CLIENT | SP_PROT_TLS1_0_CLIENT;
				break;
			}
			PCCERT_CONTEXT pCertContext = NULL;
			if (x509Cert != nullptr) pCertContext = reinterpret_cast<PCCERT_CONTEXT>(x509Cert->Handle.ToPointer());
			return m_StreamSSL->Initialize((WCHAR*)ServerName, _SslProtocol,pCertContext);
		}

		//��ʼ������ˣ�ʹ��ָ��֤��
		bool Initialize(X509Certificate2^ x509Cert)
		{
			if (x509Cert == nullptr)
			{
				throw gcnew Exception("StreamSSL::Initialize() 'x509Cert' is null");
			}
			PCCERT_CONTEXT pCertContext = reinterpret_cast<PCCERT_CONTEXT>(x509Cert->Handle.ToPointer());
			return m_StreamSSL->Initialize(pCertContext);
		}

/*
		bool AuthenticateAsClient(array<Byte>^ buffer, int offset, int count)
		{
			if (buffer == nullptr || count <= 0) return false;
			BYTE* data = new BYTE[count];
			Marshal::Copy(buffer, offset, IntPtr(data), count); // This doesn't work
			return m_StreamSSL->AuthenticateAsClient(data, count);
		}

		bool AuthenticateAsServer(array<Byte>^ buffer, int offset, int count)
		{
			if (buffer == nullptr || count <= 0) return false;
			BYTE* data = new BYTE[count];
			Marshal::Copy(buffer, offset, IntPtr(data), count); // This doesn't work
			return m_StreamSSL->AuthenticateAsServer(data, count);
		}

		bool WaitForAuthenticate()
		{
			return m_StreamSSL->WaitForAuthenticate();
		}
*/
		//�ı����ݼ��ܺ���,Ĭ��ʹ��UTF8����
		bool Encrypt(String^ message)
		{
			array<Byte>^buffer = Encoding::UTF8->GetBytes(message);
			return Encrypt(buffer, 0, buffer->Length);
		}

		//�ı����ݼ��ܺ���,ʹ��ָ������
		bool Encrypt(String^ message, Encoding^ encoding)
		{
			array<Byte>^buffer = encoding->GetBytes(message);
			return Encrypt(buffer, 0, buffer->Length);
		}

		///<summary>
		///���ݼ��ܺ���
		///<summary/>
		bool Encrypt(array<Byte>^ buffer)
		{
			return Encrypt(buffer, 0, buffer->Length);
		}
		
		//���ݼ��ܺ���
		bool Encrypt(array<Byte>^ buffer, int count)
		{
			return Encrypt(buffer, 0, count);
		}

		//���ݼ��ܺ���
		bool Encrypt(array<Byte>^ buffer, int offset, int count)
		{
			if (buffer == nullptr)
			{
				throw gcnew Exception("StreamSSL::Encrypt() 'buffer' is null");
			}
			if (offset < 0 || offset > buffer->Length)
			{
				throw gcnew Exception("StreamSSL::Encrypt() 'offset' out of range");
			}
			if (count < 0 || offset + count > buffer->Length)
			{
				throw gcnew Exception("StreamSSL::Encrypt() 'count' out of range");
			}
			if (count == 0) return true; //count==0��Ϊ�Ϸ�

			if (!m_StreamSSL->IsAuthenticated)
			{
				if (!m_StreamSSL->WaitForAuthenticate())
				{
					throw gcnew Exception("StreamSSL Authenticate failed");
				}
			}
			BYTE* data = new BYTE[count];
			Marshal::Copy(buffer, offset, IntPtr(data), count);
			return m_StreamSSL->EncryptData(data, count);
		}

		//���ݽ��ܺ���
		bool Decrypt(array<Byte>^ buffer)
		{
			return Decrypt(buffer, 0, buffer->Length);
		}

		//���ݽ��ܺ���
		bool Decrypt(array<Byte>^ buffer, int count)
		{
			return Decrypt(buffer, 0, count);
		}

		//���ݽ��ܺ���
		bool Decrypt(array<Byte>^ buffer, int offset, int count)
		{
			if (buffer == nullptr)
			{
				throw gcnew Exception("StreamSSL::Decrypt() 'buffer' is null");
			}
			if (offset < 0 || offset > buffer->Length)
			{
				throw gcnew Exception("StreamSSL::Decrypt() 'offset' out of range");
			}
			if (count < 0 || offset + count > buffer->Length)
			{
				throw gcnew Exception("StreamSSL::Decrypt() 'count' out of range");
			}
			if (count == 0) return true; //count==0��Ϊ�Ϸ�
			
			BYTE* data = new BYTE[count];
			Marshal::Copy(buffer, offset, IntPtr(data), count); // This doesn't work
			if (!m_StreamSSL->IsAuthenticated)
			{
				if (m_InitializeAsClient)  return m_StreamSSL->AuthenticateAsClient(data, count);
				else return m_StreamSSL->AuthenticateAsServer(data, count);
			}
			else return m_StreamSSL->DecryptData(data, count);
		}

		void Shutdown()
		{
			m_StreamSSL->Shutdown();
		}

		void Close()
		{
			m_StreamSSL->Close();
		}

	private:
		//��֤������ݰ���ͨ��EncryptDataOutput�ύʹ����
		bool SslTokenOutputCallback(BYTE* buffer, int count)
		{
			if (EncryptDataOutput == nullptr) return false;
			array<Byte>^ gcbuf = gcnew array<Byte>(count);
			Marshal::Copy((IntPtr)buffer, gcbuf, 0, count);
			return EncryptDataOutput(gcbuf, 0, count);
		}

		//����������ݰ���ͨ��EncryptDataOutput�ύʹ����
		bool SslEncryptDataOutputCallback(BYTE* buffer, int count)
		{
			if (EncryptDataOutput == nullptr) return false;
			array<Byte>^ gcbuf = gcnew array<Byte>(count);
			Marshal::Copy((IntPtr)buffer, gcbuf, 0, count);
			return EncryptDataOutput(gcbuf, 0, count);
		}

		//����������ݰ���ͨ��DecryptDataOutput�ύʹ����
		bool SslDecryptDataOutputCallback(BYTE* buffer, int count)
		{
			if (DecryptDataOutput == nullptr) return false;
			array<Byte>^ gcbuf = gcnew array<Byte>(count);
			Marshal::Copy((IntPtr)buffer, gcbuf, 0, count);
			return DecryptDataOutput(gcbuf, 0, count);
		}
	};
}
