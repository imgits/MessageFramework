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
		
		SslOutputDelegate^   m_SslDecryptOutputDelegate;

		array<Byte>^		 m_EncryptBuffer;
		array<Byte>^		 m_DecryptBuffer;

		Object^				 m_EncryptLock;
		Object^				 m_DecryptLock;
	public:
		SslOutputHandler^ DecryptOutput;

		//认证已完成:return true
		//认证没有完成或者失败:return false
		property bool IsAuthenticated
		{
			bool get() { return m_StreamSSL->IsAuthenticated;}
		}
		
	public:
		StreamSSL()
		{
			//TokenOutput = nullptr;
			DecryptOutput = nullptr;
			
			m_EncryptLock = gcnew Object();
			m_DecryptLock = gcnew Object();

			Object^				 m_DecryptLock;

			//构建本地 _StreamSSL 对象
			m_StreamSSL = new _StreamSSL();
			
			m_EncryptBuffer = gcnew array<Byte>(4094);
			m_DecryptBuffer = gcnew array<Byte>(4094);

			//设置回调函数_StreamSSL::DecryptOutput
			m_SslDecryptOutputDelegate = gcnew SslOutputDelegate(this, &StreamSSL::SslDecryptOutputCallback);
			IntPtr ptr = Marshal::GetFunctionPointerForDelegate(m_SslDecryptOutputDelegate);
			SslOutputCallback cb = static_cast<SslOutputCallback>(ptr.ToPointer());
			m_StreamSSL->DecryptOutput = cb;
		}
		
		~StreamSSL()
		{
			m_StreamSSL->Shutdown();
			m_StreamSSL->Close();
			delete m_StreamSSL;
			m_StreamSSL = NULL;
		}

		//初始化服务端，使用指定证书
		bool AuthenticateAsClient(Socket^ socket, SslProtocols SslProtocol)
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

		bool AuthenticateAsServer(Socket ^ socket, X509Certificate2^ x509Cert)
		{
			SOCKET _socket = (SOCKET)socket->Handle.ToPointer();
			PCCERT_CONTEXT pCertContext = reinterpret_cast<PCCERT_CONTEXT>(x509Cert->Handle.ToPointer());
			return m_StreamSSL->AuthenticateAsServer(_socket, pCertContext);
		}

		//bool WaitForAuthenticate()
		//{
		//	return m_StreamSSL->WaitForAuthenticate();
		//}

		//文本数据加密函数,默认使用UTF8编码
		bool Encrypt(String^ message)
		{
			array<Byte>^buffer = Encoding::UTF8->GetBytes(message);
			return Encrypt(buffer, 0, buffer->Length);
		}

		//文本数据加密函数,使用指定编码
		bool Encrypt(String^ message, Encoding^ encoding)
		{
			array<Byte>^buffer = encoding->GetBytes(message);
			return Encrypt(buffer, 0, buffer->Length);
		}

		///<summary>
		///数据加密函数
		///<summary/>
		bool Encrypt(array<Byte>^ buffer)
		{
			return Encrypt(buffer, 0, buffer->Length);
		}
		
		//数据加密函数
		bool Encrypt(array<Byte>^ buffer, int count)
		{
			return Encrypt(buffer, 0, count);
		}

		//数据加密函数
		bool  Encrypt(array<Byte>^ buffer, int offset, int count)
		{
			if (!m_StreamSSL->IsAuthenticated) return false;
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
			if (count == 0) return true; //count==0视为合法

			
			try
			{
				msclr::lock l(m_EncryptLock); //确保线程安全，使加密数据串行化，
				BYTE* data = new BYTE[count];
				Marshal::Copy(buffer, offset, IntPtr(data), count);

				return m_StreamSSL->EncryptData(data, count);
			}
			catch(...)
			{
				throw gcnew Exception("StreamSSL::Encrypt failed");
			}
		}

		//数据解密函数
		bool Decrypt(array<Byte>^ buffer)
		{
			return Decrypt(buffer, 0, buffer->Length);
		}

		//数据解密函数
		bool Decrypt(array<Byte>^ buffer, int count)
		{
			return Decrypt(buffer, 0, count);
		}

		//数据解密函数,包括token数据
		bool Decrypt(array<Byte>^ buffer, int offset, int count)
		{
			if (!m_StreamSSL->IsAuthenticated) return false;

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
			if (count == 0) return true; //count==0视为合法
			//try
			{
				msclr::lock l(m_DecryptLock);//确保线程安全，使解密数据串行化，
				BYTE* data = new BYTE[count];
				Marshal::Copy(buffer, offset, IntPtr(data), count); // This doesn't work
				return m_StreamSSL->DecryptData(data, count);
			}
			//catch (...)
			{
				//throw gcnew Exception("StreamSSL::Decrypt failed");
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
		//解密输出数据包，通过DecryptOutput提交给使用者
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
