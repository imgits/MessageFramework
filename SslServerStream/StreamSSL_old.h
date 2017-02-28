// StreamSSL.h
#pragma once
#include "_StreamSSL.h"
#include <vcclr.h>
#include <msclr\lock.h>

using namespace System;
using namespace System::IO;
using namespace System::Text;
using namespace System::Runtime::InteropServices;
using namespace System::Security::Cryptography::X509Certificates;
using namespace System::Security::Authentication;
using namespace System::Threading;
using namespace System::Net::Sockets;

private delegate bool  SslOutputDelegate(BYTE* buffer, int count);

public  delegate bool  SslOutputHandler(array<Byte>^buffer, int offset,int count);

namespace SecStream
{
	public ref class StreamSSL
	{
	private:
		_StreamSSL  *m_StreamSSL;
		
		SslOutputDelegate^   m_SslTokenOutputDelegate;
		SslOutputDelegate^   m_SslEncryptOutputDelegate;
		SslOutputDelegate^   m_SslDecryptOutputDelegate;
		bool				 m_InitAsClient;

		array<Byte>^		 m_EncryptBuffer;
		array<Byte>^		 m_DecryptBuffer;
		//
		//int					 m_EncryptOffset;
		//int					 m_DecryptOffset;
		//
		//int					 m_EncryptCount;
		//int					 m_DecryptCount;

		Object^				 m_EncryptLock;
		Object^				 m_DecryptLock;
	public:
		//SslOutputHandler^ TokenOutput;
		SslOutputHandler^ EncryptOutput;
		SslOutputHandler^ DecryptOutput;

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
			EncryptOutput = nullptr;
			DecryptOutput = nullptr;
			
			m_EncryptLock = gcnew Object();
			m_DecryptLock = gcnew Object();

			Object^				 m_DecryptLock;

			m_InitAsClient = false;

			//�������� _StreamSSL ����
			m_StreamSSL = new _StreamSSL();
			
			m_EncryptBuffer = gcnew array<Byte>(4094);
			m_DecryptBuffer = gcnew array<Byte>(4094);

			//���ûص�����_StreamSSL::TokenOutput
			m_SslTokenOutputDelegate = gcnew SslOutputDelegate(this, &StreamSSL::SslTokenOutputCallback);
			IntPtr ptr = Marshal::GetFunctionPointerForDelegate(m_SslTokenOutputDelegate);
			SslOutputCallback cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_StreamSSL->TokenOutput = cb;

			//���ûص�����_StreamSSL::EncryptOutput
			m_SslEncryptOutputDelegate = gcnew SslOutputDelegate(this, &StreamSSL::SslEncryptOutputCallback);
			ptr = Marshal::GetFunctionPointerForDelegate(m_SslEncryptOutputDelegate);
			cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_StreamSSL->EncryptOutput = cb;

			//���ûص�����_StreamSSL::DecryptOutput
			m_SslDecryptOutputDelegate = gcnew SslOutputDelegate(this, &StreamSSL::SslDecryptOutputCallback);
			ptr = Marshal::GetFunctionPointerForDelegate(m_SslDecryptOutputDelegate);
			cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_StreamSSL->DecryptOutput = cb;
		}
		
		~StreamSSL()
		{
			m_StreamSSL->Shutdown();
			m_StreamSSL->Close();
			delete m_StreamSSL;
			m_StreamSSL = NULL;
		}

		//��ʼ���ͻ��ˣ�Ĭ��ʹ��SSL3.0��TLS1.0Э�飬��֤��
		bool InitAsClient(String^ TargetHost)
		{
			return InitAsClient(TargetHost, SslProtocols::Default, nullptr);
		}

		//��ʼ���ͻ��ˣ�ʹ��ָ��Э�飬��֤��
		bool InitAsClient(String^ TargetHost, SslProtocols Protocol)
		{
			return InitAsClient(TargetHost, Protocol, nullptr);
		}

		//��ʼ���ͻ��ˣ�ʹ��ָ��Э���֤��
		bool InitAsClient(String^ TargetHost, SslProtocols Protocol, X509Certificate2^ x509Cert)
		{
			m_InitAsClient = true;
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
			return m_StreamSSL->InitAsClient((WCHAR*)ServerName, _SslProtocol,pCertContext);
		}

		//��ʼ������ˣ�ʹ��ָ��֤��
		bool InitAsServer(X509Certificate2^ x509Cert)
		{
			if (x509Cert == nullptr)
			{
				throw gcnew Exception("StreamSSL::Initialize() 'x509Cert' is null");
			}
			m_InitAsClient = false;
			PCCERT_CONTEXT pCertContext = reinterpret_cast<PCCERT_CONTEXT>(x509Cert->Handle.ToPointer());
			return m_StreamSSL->InitAsServer(pCertContext);
		}


		bool AuthenticateAsClient(Socket^ socket,SslProtocols SslProtocol)
		{
			SOCKET _socket = (SOCKET)socket->Handle.ToPointer();
			DWORD _SslProtocol = 0;
			//int iProtocol = (int)(System::Int32^)Protocol;
			int iProtocol = (int)SslProtocol;
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
			return m_StreamSSL->AuthenticateAsClient(_socket,_SslProtocol);
		}

		bool AuthenticateAsServer(Socket^ socket, X509Certificate2^ x509Cert)
		{
			SOCKET _socket = (SOCKET)socket->Handle.ToPointer();
			PCCERT_CONTEXT pCertContext = reinterpret_cast<PCCERT_CONTEXT>(x509Cert->Handle.ToPointer());
			return m_StreamSSL->AuthenticateAsServer(_socket, pCertContext);
		}

		//bool WaitForAuthenticate()
		//{
		//	return m_StreamSSL->WaitForAuthenticate();
		//}

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
		bool  Encrypt(array<Byte>^ buffer, int offset, int count)
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

			if (!m_StreamSSL->IsAuthenticated) return false;
			try
			{
				msclr::lock l(m_EncryptLock); //ȷ���̰߳�ȫ��ʹ�������ݴ��л���
				BYTE* data = new BYTE[count];
				Marshal::Copy(buffer, offset, IntPtr(data), count);

				return m_StreamSSL->EncryptData(data, count);
			}
			catch(...)
			{
				throw gcnew Exception("StreamSSL::Encrypt failed");
			}
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

		//���ݽ��ܺ���,����token����
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
			try
			{
				msclr::lock l(m_DecryptLock);//ȷ���̰߳�ȫ��ʹ�������ݴ��л���
				BYTE* data = new BYTE[count];
				Marshal::Copy(buffer, offset, IntPtr(data), count); // This doesn't work
				if (!m_StreamSSL->IsAuthenticated)
				{
					if (m_InitAsClient)
					{
						return m_StreamSSL->AuthenticateAsClient(data, count);
					}
					else
					{
						return m_StreamSSL->AuthenticateAsServer(data, count);
					}
				}
				else return m_StreamSSL->DecryptData(data, count);
			}
			catch (...)
			{
				throw gcnew Exception("StreamSSL::Decrypt failed");
			}
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
		//��֤������ݰ���ͨ��EncryptOutput�ύ��ʹ����
		bool SslTokenOutputCallback(BYTE* buffer, int count)
		{
			return SslEncryptOutputCallback(buffer, count);
		}

		//����������ݰ���ͨ��EncryptOutput�ύ��ʹ����
		bool SslEncryptOutputCallback(BYTE* buffer, int count)
		{
			if (EncryptOutput == nullptr) return false;
			while (count > 0)
			{
				int bytes = count <= m_EncryptBuffer->Length ? count : m_EncryptBuffer->Length;
				Marshal::Copy((IntPtr)buffer, m_EncryptBuffer, 0, bytes);
				if (!EncryptOutput(m_EncryptBuffer, 0, bytes)) return false;
				buffer += bytes;
				count -= bytes;
			}
			return true;
		}

		//����������ݰ���ͨ��DecryptOutput�ύ��ʹ����
		bool SslDecryptOutputCallback(BYTE* buffer, int count)
		{
			while (count > 0)
			{
				int bytes = count <= m_DecryptBuffer->Length ? count : m_DecryptBuffer->Length;
				Marshal::Copy((IntPtr)buffer, m_DecryptBuffer, 0, bytes);
				if (!DecryptOutput(m_DecryptBuffer, 0, bytes)) return false;
				buffer += bytes;
				count -= bytes;
			}
			return true;
		}
	};
}
